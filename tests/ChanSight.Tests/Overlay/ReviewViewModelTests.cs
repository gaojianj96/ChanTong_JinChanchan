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
}