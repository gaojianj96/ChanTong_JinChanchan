using ChanSight.Core.Annotation;
using ChanSight.Core.FrameStorage;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.Services;
using ChanSight.Overlay.ViewModels;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Overlay;

public sealed class ReviewViewModelTests
{
    [Fact]
    public async Task ApplyCorrectionThenBuildDecisions_LinksCorrectionRecordToDecision()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ChanSight_ReviewVM_{Guid.NewGuid():N}");
        try
        {
            var archive = new FrameArchive(root);
            var store = new AnnotationStore(Path.Combine(root, "corrections"));
            var recognizer = new FakeRecognizer { NextFrame = Recognition(board0: "盖伦") };
            var service = new ReviewService(archive, store, recognizer.RecognizeAsync);
            var vm = new ReviewViewModel(service, store, new RoiMapperService());

            using (var mat = CreateMat())
            {
                archive.StoreKeyFrame(mat, "match-1", snapshotVersion: 1);
            }

            vm.MatchId = "match-1";
            vm.LoadFramesCommand.Execute(null);
            vm.KeyFrames.Should().HaveCount(1);

            vm.SelectedFrame = vm.KeyFrames[0];
            vm.Cells.Should().HaveCount(42);
            vm.Cells[0].RecognizedName.Should().Be("盖伦");

            vm.SelectedCell = vm.Cells[0];
            vm.SelectedHero = "厄斐琉斯";
            vm.SelectedCorrectionTypeIndex = 0;
            await vm.ApplyCorrectionCommand.ExecuteAsync(null);

            vm.Cells[0].IsCorrected.Should().BeTrue();
            vm.Cells[0].CorrectedHero.Should().Be("厄斐琉斯");
            vm.Cells[0].CorrectionId.Should().NotBeNullOrWhiteSpace();

            var decisions = vm.BuildDecisions();
            decisions.Should().ContainSingle();
            decisions[0].RegionType.Should().Be(RegionTypes.BoardHero);
            decisions[0].CellIndex.Should().Be(0);
            decisions[0].CorrectionId.Should().Be(vm.Cells[0].CorrectionId);

            using (var doc = System.Text.Json.JsonDocument.Parse(decisions[0].CorrectedValue))
            {
                doc.RootElement.GetProperty("hero").GetString().Should().Be("厄斐琉斯");
            }

            var corrections = await store.LoadCorrectionsAsync("match-1");
            corrections.Should().ContainSingle(c => c.Id == decisions[0].CorrectionId);
            corrections[0].FrameId.Should().Be(vm.SelectedFrame!.FrameId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void MatchIdOptions_EnumeratesExistingMatchDirectories()
    {
        using var fx = new Fixture();
        using (var mat = CreateMat())
        {
            fx.Archive.StoreKeyFrame(mat, "match-a", snapshotVersion: 1);
        }

        using (var mat = CreateMat())
        {
            fx.Archive.StoreKeyFrame(mat, "match-b", snapshotVersion: 1);
        }

        fx.Vm.RefreshMatchesCommand.Execute(null);

        fx.Vm.MatchIdOptions.Should().Equal("match-a", "match-b");
    }

    [Fact]
    public void ZoomBy_ClampsBetweenHalfAndFourX()
    {
        using var fx = new Fixture();

        fx.Vm.ZoomBy(1000.0);
        fx.Vm.Zoom.Should().Be(4.0);

        fx.Vm.ZoomBy(0.000001);
        fx.Vm.Zoom.Should().Be(0.5);
    }

    [Fact]
    public async Task CorrectionTypeOptions_UseChineseLabels_AndMapBackToEnum()
    {
        using var fx = new Fixture(Recognition(board0: "盖伦"));
        fx.StoreFrame();
        fx.Vm.MatchId = "match-1";
        fx.Vm.LoadFramesCommand.Execute(null);
        fx.Vm.SelectedFrame = fx.Vm.KeyFrames[0];

        fx.Vm.CorrectionTypeOptions.Should().Equal("分类错", "定位错", "漏检", "误检");

        fx.Vm.SelectedCell = fx.Vm.BoardCells[0];
        fx.Vm.SelectedHero = "厄斐琉斯";
        fx.Vm.SelectedCorrectionTypeIndex = 2;
        await fx.Vm.ApplyCorrectionCommand.ExecuteAsync(null);

        fx.Vm.BoardCells[0].CorrectionType.Should().Be(CorrectionTypes.Missed);
    }

    [Fact]
    public void LoadFrame_PopulatesBoardAndSideCells_WithTooltipDetails()
    {
        using var fx = new Fixture(Recognition(board0: "盖伦"));
        fx.StoreFrame();
        fx.Vm.MatchId = "match-1";
        fx.Vm.LoadFramesCommand.Execute(null);
        fx.Vm.SelectedFrame = fx.Vm.KeyFrames[0];

        fx.Vm.BoardCells.Should().HaveCount(28);
        fx.Vm.SideCells.Should().HaveCount(14);

        var board0 = fx.Vm.BoardCells[0];
        board0.ShortLabel.Should().Be("盖伦★1");
        board0.ToolTipText.Should().Contain("英雄: 盖伦");
        board0.ToolTipText.Should().Contain("来源 tier: T2");
        board0.ToolTipText.Should().Contain("置信度: 90%");
    }

    [Fact]
    public void LoadFrame_BoardCellTooltip_ContainsNameStarItemsTierConfidence()
    {
        var board = Enumerable.Range(0, 28).Select(_ => EmptyCell()).ToList();
        board[0] = new UnitCell("盖伦", 2, new[] { new ItemStack("无尽之刃", 2) }, 0.95, SourceTier.T1);
        var bench = Enumerable.Range(0, 9).Select(_ => EmptyCell()).ToList();
        var shop = Enumerable.Range(0, 5).Select(_ => new ShopCard(string.Empty, 0, 0.0, SourceTier.T0)).ToList();

        using var fx = new Fixture(new RecognitionFrame { BoardCells = board, BenchCells = bench, ShopCards = shop });
        fx.StoreFrame();
        fx.Vm.MatchId = "match-1";
        fx.Vm.LoadFramesCommand.Execute(null);
        fx.Vm.SelectedFrame = fx.Vm.KeyFrames[0];

        var tip = fx.Vm.BoardCells[0].ToolTipText;
        tip.Should().Contain("英雄: 盖伦");
        tip.Should().Contain("星级: 2");
        tip.Should().Contain("装备: 无尽之刃×2");
        tip.Should().Contain("来源 tier: T1");
        tip.Should().Contain("置信度: 95%");
        fx.Vm.BoardCells[0].ShortLabel.Should().Be("盖伦★2");
    }

    private static RecognitionFrame Recognition(string? board0 = null)
    {
        var board = Enumerable.Range(0, 28).Select(_ => EmptyCell()).ToList();
        var bench = Enumerable.Range(0, 9).Select(_ => EmptyCell()).ToList();
        var shop = Enumerable.Range(0, 5).Select(_ => new ShopCard(string.Empty, 0, 0.0, SourceTier.T0)).ToList();
        if (board0 is not null)
        {
            board[0] = new UnitCell(board0, 1, Array.Empty<ItemStack>(), 0.9, SourceTier.T2);
        }

        return new RecognitionFrame { BoardCells = board, BenchCells = bench, ShopCards = shop };
    }

    private static UnitCell EmptyCell() => new(null, 0, Array.Empty<ItemStack>(), 0.0, SourceTier.T0);

    private static Mat CreateMat()
    {
        var mat = new Mat(48, 64, MatType.CV_8UC3);
        mat.SetTo(new Scalar(10, 20, 30));
        return mat;
    }

    private sealed class FakeRecognizer
    {
        public RecognitionFrame NextFrame { get; set; } = Recognition();

        public Task<RecognitionFrame> RecognizeAsync(Mat frame, CancellationToken ct) =>
            Task.FromResult(NextFrame);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"ChanSight_ReviewVM_{Guid.NewGuid():N}");
        public FrameArchive Archive { get; }
        public AnnotationStore Store { get; }
        public FakeRecognizer Recognizer { get; } = new();
        public ReviewService Service { get; }
        public ReviewViewModel Vm { get; }

        public Fixture(RecognitionFrame? next = null)
        {
            Recognizer.NextFrame = next ?? Recognition();
            Archive = new FrameArchive(Root);
            Store = new AnnotationStore(Path.Combine(Root, "corrections"));
            Service = new ReviewService(Archive, Store, Recognizer.RecognizeAsync);
            Vm = new ReviewViewModel(Service, Store, new RoiMapperService());
        }

        public void StoreFrame(long snapshotVersion = 1)
        {
            using var mat = CreateMat();
            Archive.StoreKeyFrame(mat, "match-1", snapshotVersion);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}