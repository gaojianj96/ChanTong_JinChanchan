using ChanSight.Core.Engine;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class RecognitionPipelineTests
{
    private const int FrameWidth = 1920;
    private const int FrameHeight = 1080;

    // ----- test 1: first frame, OCR + VLM populate all fields -----
    [Fact]
    public async Task RecognizeAsync_FirstFrame_PopulatesGoldStageAndHeroName()
    {
        var vlm = new ScriptedVlmClient().QueueResult(AllCellsJson("盖伦"));
        var pipeline = CreatePipeline(
            vlm,
            gold: "123",
            stage: "3-1",
            phase: GamePhase.Planning,
            occupiedBoardCells: new[] { 0 });

        using var frame = CreateSolidFrame();
        var result = await pipeline.RecognizeAsync(frame);

        result.Gold.Should().Be(123);
        result.Stage.Should().Be("3-1");
        result.BoardCells.Should().HaveCount(28);
        result.BenchCells.Should().HaveCount(9);
        result.BoardCells[0].Name.Should().Be("盖伦");
        result.BoardCells[0].Star.Should().Be(2);
        result.Phase.Should().Be(GamePhase.Planning);
    }

    // ----- test 2: version gating - stale VLM result does not overwrite newer frame -----
    [Fact]
    public async Task RecognizeAsync_SlowFirstFrame_DoesNotOverwriteNewerFrame()
    {
        var vlm = new PendingVlmClient();
        var firstPending = vlm.EnqueuePending();
        var secondPending = vlm.EnqueuePending();
        var pipeline = CreatePipeline(
            vlm,
            occupiedBoardCells: new[] { 0 });

        using var frame1 = CreateSolidFrame();
        using var frame2 = CreateSolidFrame();

        var first = pipeline.RecognizeAsync(frame1);
        var second = pipeline.RecognizeAsync(frame2);

        // Second frame's VLM resolves first.
        secondPending.SetResult(AllCellsJson("盖伦"));
        var result2 = await second;

        // First frame's VLM resolves afterwards; it is now stale and must be discarded.
        firstPending.SetResult(AllCellsJson("盖伦"));
        var result1 = await first;

        result2.BoardCells[0].Name.Should().Be("盖伦");
        result2.BoardCells[0].SourceTier.Should().Be(SourceTier.T2);

        // Frame 1's late VLM is gated out: its cell degrades rather than backfilling.
        result1.BoardCells[0].Name.Should().BeNull();
        result1.BoardCells[0].SourceTier.Should().Be(SourceTier.T3);
    }

    // ----- test 3: only changed cells trigger a VLM call; unchanged cells carry forward -----
    [Fact]
    public async Task RecognizeAsync_OnlyChangedCellInvokesVlm()
    {
        var vlm = new ScriptedVlmClient()
            .QueueResult(AllCellsJson("盖伦"))   // frame 1
            .QueueResult(AllCellsJson("亚索"))   // frame 2
            .QueueResult(AllCellsJson("亚索"));  // frame 3
        var gridSlicer = new FakeGridSlicer();
        gridSlicer.SetBoardCrop(0, CreateBlobCrop(2));
        gridSlicer.SetBoardCrop(5, CreateBlobCrop(2));

        var pipeline = CreatePipeline(vlm, gridSlicer);

        using var frame1 = CreateSolidFrame();
        using var frame2 = CreateSolidFrame();
        DrawBlockInCell(frame2, 5, 128);
        using var frame3 = frame2.Clone();
        DrawBlockInCell(frame3, 5, 200);

        await pipeline.RecognizeAsync(frame1);
        await pipeline.RecognizeAsync(frame2);

        int callsBefore = vlm.CallCount;
        var result3 = await pipeline.RecognizeAsync(frame3);

        // Only the single changed cell (5) is sent to the VLM; cell 0 carries forward.
        vlm.CallCount.Should().Be(callsBefore + 1);
        vlm.LastImageCount.Should().Be(1);
        result3.BoardCells[0].Name.Should().Be("盖伦");
        result3.BoardCells[5].Name.Should().Be("亚索");
    }

    // ----- test 4: VLM failure degrades names but keeps deterministic fields -----
    [Fact]
    public async Task RecognizeAsync_VlmFailure_DegradesNameButKeepsDeterministicFields()
    {
        var vlm = new ScriptedVlmClient()
            .QueueException(new VlmUnavailableException("down"))
            .QueueException(new VlmUnavailableException("down"));
        var pipeline = CreatePipeline(
            vlm,
            gold: "77",
            stage: "2-4",
            occupiedBoardCells: new[] { 0 });

        using var frame = CreateSolidFrame();
        var result = await pipeline.RecognizeAsync(frame);

        result.Gold.Should().Be(77);
        result.Stage.Should().Be("2-4");
        result.BoardCells[0].Name.Should().BeNull();
        result.BoardCells[0].SourceTier.Should().Be(SourceTier.T3);
    }

    // ----- test 5: star counts map onto UnitCell.Star -----
    [Fact]
    public async Task RecognizeAsync_StarCount_MapsToUnitCellStar()
    {
        var vlm = new ScriptedVlmClient();
        var gridSlicer = new FakeGridSlicer();
        gridSlicer.SetBoardCrop(3, CreateBlobCrop(2));

        var pipeline = CreatePipeline(vlm, gridSlicer);

        using var frame = CreateSolidFrame();
        var result = await pipeline.RecognizeAsync(frame);

        result.BoardCells[3].Star.Should().Be(2);
        result.BoardCells[0].Star.Should().Be(0);
    }

    // ----- test 6: phase detector result flows onto the frame -----
    [Fact]
    public async Task RecognizeAsync_Phase_ComesFromPhaseDetector()
    {
        var vlm = new ScriptedVlmClient();
        var pipeline = CreatePipeline(vlm, phase: GamePhase.Combat);

        using var frame = CreateSolidFrame();
        var result = await pipeline.RecognizeAsync(frame);

        result.Phase.Should().Be(GamePhase.Combat);
    }

    // ----- test 7: empty frame returns a default frame without throwing -----
    [Fact]
    public async Task RecognizeAsync_EmptyFrame_ReturnsDefaultFrame()
    {
        var vlm = new ScriptedVlmClient();
        var pipeline = CreatePipeline(vlm);

        using var empty = new Mat();
        var result = await pipeline.RecognizeAsync(empty);

        result.Should().NotBeNull();
        result.BoardCells.Should().BeEmpty();
        result.Gold.Should().Be(0);
    }

    [Fact]
    public async Task RecognizeAsync_NullFrame_ReturnsDefaultFrame()
    {
        var vlm = new ScriptedVlmClient();
        var pipeline = CreatePipeline(vlm);

        var result = await pipeline.RecognizeAsync(null!);

        result.Should().NotBeNull();
        result.BoardCells.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- helpers

    private static RecognitionPipeline CreatePipeline(
        ScriptedVlmClient vlm,
        FakeGridSlicer? gridSlicer = null,
        string? gold = null,
        string? stage = null,
        GamePhase? phase = null,
        IReadOnlyList<int>? occupiedBoardCells = null)
    {
        gridSlicer ??= new FakeGridSlicer();
        if (occupiedBoardCells is not null)
        {
            foreach (var index in occupiedBoardCells)
                gridSlicer.SetBoardCrop(index, CreateBlobCrop(2));
        }

        var adapter = new VlmRecognitionAdapter(vlm);
        var fusion = new FusionArbitrator(vlm);

        return new RecognitionPipeline(
            new FakeOcr(gold, stage),
            new LocalDeterministicRecognizer(),
            new CellChangeDetector(),
            adapter,
            fusion,
            new FakePhaseDetector(phase ?? GamePhase.Planning),
            gridSlicer);
    }

    private static RecognitionPipeline CreatePipeline(PendingVlmClient vlm, IReadOnlyList<int>? occupiedBoardCells = null)
    {
        var gridSlicer = new FakeGridSlicer();
        if (occupiedBoardCells is not null)
        {
            foreach (var index in occupiedBoardCells)
                gridSlicer.SetBoardCrop(index, CreateBlobCrop(2));
        }

        var adapter = new VlmRecognitionAdapter(vlm);
        var fusion = new FusionArbitrator(vlm);

        return new RecognitionPipeline(
            new FakeOcr(null, null),
            new LocalDeterministicRecognizer(),
            new CellChangeDetector(),
            adapter,
            fusion,
            new FakePhaseDetector(GamePhase.Planning),
            gridSlicer);
    }

    private static Mat CreateSolidFrame()
    {
        var mat = new Mat(FrameHeight, FrameWidth, MatType.CV_8UC3);
        mat.SetTo(Scalar.Black);
        return mat;
    }

    private static Mat CreateBlobCrop(int blobCount)
    {
        const int width = 160;
        const int height = 160;
        const int radius = 30;
        var canvas = new Mat(height, width, MatType.CV_8UC3);
        canvas.SetTo(Scalar.Black);

        var centers = new[]
        {
            new Point(50, 50),
            new Point(110, 110),
        };

        for (int i = 0; i < blobCount && i < centers.Length; i++)
        {
            Cv2.Circle(canvas, centers[i], radius, new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);
        }

        return canvas;
    }

    private static void DrawBlockInCell(Mat frame, int flatIndex, byte grayValue)
    {
        const double blockFraction = 0.7;
        var g = BoardGeometry.CreateCanonical();

        double centerX;
        double centerY;
        double blockWidth;
        double blockHeight;

        if (flatIndex < g.BoardCells.Count)
        {
            var cell = g.BoardCells[flatIndex];
            centerX = cell.X;
            centerY = cell.Y;
            blockWidth = g.ColPitchX * blockFraction;
            blockHeight = g.RowPitchY * blockFraction;
        }
        else
        {
            var cell = g.BenchCells[flatIndex - g.BoardCells.Count];
            centerX = cell.X;
            centerY = cell.Y;
            double pitch = g.BenchCells[1].X - g.BenchCells[0].X;
            blockWidth = pitch * blockFraction;
            blockHeight = pitch * blockFraction;
        }

        int x0 = Math.Max(0, (int)Math.Round(centerX - blockWidth / 2.0));
        int y0 = Math.Max(0, (int)Math.Round(centerY - blockHeight / 2.0));
        int x1 = Math.Min(frame.Width, (int)Math.Round(centerX + blockWidth / 2.0));
        int y1 = Math.Min(frame.Height, (int)Math.Round(centerY + blockHeight / 2.0));

        if (x1 <= x0 || y1 <= y0)
            return;

        Cv2.Rectangle(frame, new Rect(x0, y0, x1 - x0, y1 - y0), new Scalar(grayValue, grayValue, grayValue), -1);
    }

    private static string AllCellsJson(string name)
    {
        var entries = Enumerable.Range(0, 37)
            .Select(i => $$"""{ "cellIndex": {{i}}, "name": "{{name}}", "star": 2, "confidence": 0.9 }""");
        return "[" + string.Join(", ", entries) + "]";
    }

    // ---------------------------------------------------------------- fakes

    private sealed class FakeOcr : IPaddleOcrService
    {
        private readonly string? _gold;
        private readonly string? _stage;

        public FakeOcr(string? gold, string? stage)
        {
            _gold = gold;
            _stage = stage;
        }

        public IReadOnlyList<DetectedShopCard> RecognizeShopCards(Mat frame) => Array.Empty<DetectedShopCard>();

        public OcrTextResult RecognizeGold(Mat frame) =>
            _gold is null
                ? new OcrTextResult(string.Empty, string.Empty, null, 0f)
                : new OcrTextResult(_gold, _gold, _gold, 1f);

        public OcrTextResult RecognizeStage(Mat frame) =>
            _stage is null
                ? new OcrTextResult(string.Empty, string.Empty, null, 0f)
                : new OcrTextResult(_stage, _stage, _stage, 1f);
    }

    private sealed class FakePhaseDetector : IPhaseDetector
    {
        private readonly GamePhase _phase;

        public FakePhaseDetector(GamePhase phase) => _phase = phase;

        public GamePhase Detect(RecognitionFrame frame) => _phase;

        public Task<GamePhase> DetectAsync(RecognitionFrame frame, CancellationToken ct = default) =>
            Task.FromResult(_phase);
    }

    private sealed class FakeGridSlicer : IGridSlicerService
    {
        private readonly Dictionary<int, Mat> _boardCrops = new();
        private readonly Dictionary<int, Mat> _benchCrops = new();

        public void SetBoardCrop(int flatIndex, Mat mat) => _boardCrops[flatIndex] = mat;

        public void SetBenchCrop(int benchIndex, Mat mat) => _benchCrops[benchIndex] = mat;

        public IReadOnlyList<BenchSlot> SliceBenchSlots(Mat frame)
        {
            var results = new List<BenchSlot>(9);
            for (int i = 0; i < 9; i++)
            {
                var image = _benchCrops.TryGetValue(i, out var mat) ? mat.Clone() : new Mat();
                results.Add(new BenchSlot(i, new Point2d(0, 0), new Rect(0, 0, 0, 0), image));
            }
            return results;
        }

        public IReadOnlyList<BoardHexSlot> SliceBoardHexagons(Mat frame)
        {
            var results = new List<BoardHexSlot>(28);
            for (int i = 0; i < 28; i++)
            {
                var image = _boardCrops.TryGetValue(i, out var mat) ? mat.Clone() : new Mat();
                results.Add(new BoardHexSlot(i / 7, i % 7, new Point2d(0, 0), new Rect(0, 0, 0, 0), image));
            }
            return results;
        }

        public Mat? CropHexCell(Mat frame, int row, int col) => null;
    }

    private sealed class PendingVlmClient : IVlmClient
    {
        public TaskCompletionSource<string> EnqueuePending()
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tasks.Enqueue(tcs);
            return tcs;
        }

        private readonly Queue<TaskCompletionSource<string>> _tasks = new();

        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct)
        {
            if (_tasks.Count == 0)
                throw new InvalidOperationException("PendingVlmClient exhausted.");
            return _tasks.Dequeue().Task;
        }
    }

    private sealed class ScriptedVlmClient : IVlmClient
    {
        private readonly Queue<object> _script = new();

        public int CallCount { get; private set; }
        public int LastImageCount { get; private set; }
        public string? LastPrompt { get; private set; }

        public ScriptedVlmClient QueueResult(string json)
        {
            _script.Enqueue(json);
            return this;
        }

        public ScriptedVlmClient QueueException(Exception exception)
        {
            _script.Enqueue(exception);
            return this;
        }

        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct)
        {
            CallCount++;
            LastImageCount = images.Count;
            LastPrompt = prompt;

            if (_script.Count == 0)
                return Task.FromResult("[]");

            var next = _script.Dequeue();
            if (next is Exception exception)
                throw exception;

            return Task.FromResult((string)next);
        }
    }
}