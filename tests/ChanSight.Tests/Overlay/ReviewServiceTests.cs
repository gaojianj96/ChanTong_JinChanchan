using ChanSight.Core.Annotation;
using ChanSight.Core.FrameStorage;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.Services;
using ChanSight.Vision.Models;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Overlay;

public sealed class ReviewServiceTests
{
    private const string MatchId = "match-1";

    [Fact]
    public async Task CommitReviewAsync_WritesOneLabelPerDecision_ConfirmCorrectHasEqualValues()
    {
        using var fx = new Fixture();
        fx.Recognizer.NextFrame = Recognition(board0: "盖伦", bench0: "拉克丝", shop0: "盖伦");
        var frameId = StoreFrame(fx);

        ReviewDecision[] decisions =
        [
            new(RegionTypes.BoardHero, 0, "盖伦", "{\"hero\":\"厄斐琉斯\"}", CorrectionTypes.Classification, "corr-1"),
            new(RegionTypes.BenchHero, 0, "拉克丝", "{\"hero\":\"亚索\"}", CorrectionTypes.Classification, "corr-2"),
            new(RegionTypes.ShopHero, 0, "盖伦", "{\"hero\":\"盖伦\"}", CorrectionTypes.ConfirmCorrect, null),
        ];

        var labels = await fx.Service.CommitReviewAsync(MatchId, frameId, decisions);

        labels.Should().HaveCount(3);
        var confirm = labels.Single(l => l.RegionType == RegionTypes.ShopHero && l.CellIndex == 0);
        confirm.CorrectionType.Should().Be(CorrectionTypes.ConfirmCorrect);
        confirm.RecognizedValue.Should().Be(confirm.CorrectedValue);
    }

    [Fact]
    public async Task CommitReviewAsync_WhenDecisionsIncomplete_FillsRestWithConfirmCorrect()
    {
        using var fx = new Fixture();
        fx.Recognizer.NextFrame = Recognition(board0: "盖伦", board1: "赛娜", board2: "金克丝");
        var frameId = StoreFrame(fx);

        ReviewDecision[] decisions =
        [
            new(RegionTypes.BoardHero, 0, "盖伦", "{\"hero\":\"厄斐琉斯\"}", CorrectionTypes.Classification, "corr-1"),
        ];

        var labels = await fx.Service.CommitReviewAsync(MatchId, frameId, decisions);

        labels.Should().HaveCount(3);
        var auto = labels.Where(l => l.CorrectionType == CorrectionTypes.ConfirmCorrect).ToList();
        auto.Should().HaveCount(2);
        auto.Should().OnlyContain(l => l.RecognizedValue == l.CorrectedValue);
    }

    [Fact]
    public async Task CommitReviewAsync_WritesCorrectionId()
    {
        using var fx = new Fixture();
        fx.Recognizer.NextFrame = Recognition(board0: "盖伦");
        var frameId = StoreFrame(fx);

        var labels = await fx.Service.CommitReviewAsync(
            MatchId,
            frameId,
            new ReviewDecision[]
            {
                new(RegionTypes.BoardHero, 0, "盖伦", "{\"hero\":\"厄斐琉斯\"}", CorrectionTypes.Classification, "corr-xyz"),
            });

        var label = labels.Single(l => l.RegionType == RegionTypes.BoardHero && l.CellIndex == 0);
        label.CorrectionId.Should().Be("corr-xyz");
    }

    [Fact]
    public async Task CommitReviewAsync_ReConfirmation_RevokesPreviousAndFiltersCorrectly()
    {
        using var fx = new Fixture();
        fx.Recognizer.NextFrame = Recognition(board0: "盖伦");
        var frameId = StoreFrame(fx);

        await fx.Service.CommitReviewAsync(
            MatchId,
            frameId,
            new ReviewDecision[]
            {
                new(RegionTypes.BoardHero, 0, "盖伦", "{\"hero\":\"厄斐琉斯\"}", CorrectionTypes.Classification, "corr-1"),
            });

        await fx.Service.CommitReviewAsync(
            MatchId,
            frameId,
            new ReviewDecision[]
            {
                new(RegionTypes.BoardHero, 0, "盖伦", "{\"hero\":\"亚索\"}", CorrectionTypes.Classification, "corr-1"),
            });

        var all = await fx.Store.LoadGoldLabelsIncludingRevokedAsync(MatchId);
        all.Should().Contain(l => l.Revoked);

        var active = await fx.Store.LoadGoldLabelsAsync(MatchId);
        active.Should().ContainSingle();
        active[0].CorrectedValue.Should().Be("{\"hero\":\"亚索\"}");
        active[0].Revoked.Should().BeFalse();
    }

    [Fact]
    public void LoadFrame_ReadsBackKeyFrameAndMeta()
    {
        using var fx = new Fixture();
        fx.Recognizer.NextFrame = Recognition(board0: "盖伦");

        using var mat = CreateMat();
        var layoutMeta = new Dictionary<string, string> { ["layoutVersion"] = "1.3" };
        var frameId = fx.Archive.StoreKeyFrame(mat, MatchId, snapshotVersion: 7, layoutMeta);

        var review = fx.Service.LoadFrame(MatchId, frameId);
        try
        {
            review.FrameId.Should().Be(frameId);
            review.FrameImage.Empty().Should().BeFalse();
            review.Meta["layoutVersion"].Should().Be("1.3");
            review.Meta["width"].Should().Be("64");
            review.Meta["height"].Should().Be("48");
            review.Recognition.Should().NotBeNull();
        }
        finally
        {
            review.FrameImage.Dispose();
        }
    }

    [Fact]
    public void ListKeyFrames_ReturnsStoredFramesAscending()
    {
        using var fx = new Fixture();
        using var m1 = CreateMat(new Scalar(1, 0, 0));
        using var m2 = CreateMat(new Scalar(2, 0, 0));
        fx.Archive.StoreKeyFrame(m1, MatchId, snapshotVersion: 10);
        fx.Archive.StoreKeyFrame(m2, MatchId, snapshotVersion: 20);

        var keys = fx.Service.ListKeyFrames(MatchId);

        keys.Should().HaveCount(2);
        keys.Select(k => k.SnapshotVersion).Should().Equal(10L, 20L);
    }

    private static string StoreFrame(Fixture fx)
    {
        using var mat = CreateMat();
        return fx.Archive.StoreKeyFrame(mat, MatchId, snapshotVersion: 1);
    }

    private static RecognitionFrame Recognition(string? board0 = null, string? board1 = null, string? board2 = null,
        string? bench0 = null, string? shop0 = null)
    {
        var board = Enumerable.Range(0, 28).Select(_ => EmptyCell()).ToList();
        var bench = Enumerable.Range(0, 9).Select(_ => EmptyCell()).ToList();
        var shop = Enumerable.Range(0, 5).Select(_ => new ShopCard(string.Empty, 0, 0.0, SourceTier.T0)).ToList();

        if (board0 is not null) board[0] = HeroCell(board0);
        if (board1 is not null) board[1] = HeroCell(board1);
        if (board2 is not null) board[2] = HeroCell(board2);
        if (bench0 is not null) bench[0] = HeroCell(bench0);
        if (shop0 is not null) shop[0] = new ShopCard(shop0, 4, 0.9, SourceTier.T2);

        return new RecognitionFrame
        {
            Gold = 10,
            Stage = "2-1",
            BoardCells = board,
            BenchCells = bench,
            ShopCards = shop,
        };
    }

    private static UnitCell EmptyCell() => new(null, 0, Array.Empty<ItemStack>(), 0.0, SourceTier.T0);

    private static UnitCell HeroCell(string name) => new(name, 1, Array.Empty<ItemStack>(), 0.9, SourceTier.T2);

    private static Mat CreateMat(Scalar? color = null)
    {
        var mat = new Mat(48, 64, MatType.CV_8UC3);
        mat.SetTo(color ?? new Scalar(10, 20, 30));
        return mat;
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"ChanSight_Review_{Guid.NewGuid():N}");
        public FrameArchive Archive { get; }
        public AnnotationStore Store { get; }
        public FakeRecognizer Recognizer { get; } = new();
        public ReviewService Service { get; }

        public Fixture()
        {
            Archive = new FrameArchive(Root);
            Store = new AnnotationStore(Path.Combine(Root, "corrections"));
            Service = new ReviewService(Archive, Store, Recognizer.RecognizeAsync);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FakeRecognizer
    {
        public RecognitionFrame NextFrame { get; set; } = Recognition();

        public Task<RecognitionFrame> RecognizeAsync(Mat frame, CancellationToken ct) =>
            Task.FromResult(NextFrame);
    }
}