using ChanSight.Core.Engine;
using ChanSight.Core.FrameStorage;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.FrameStorage;

public sealed class FrameArchiveTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FrameArchive _archive;

    public FrameArchiveTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ChanSight_FrameArchive_{Guid.NewGuid():N}");
        _archive = new FrameArchive(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void StoreKeyFrame_ReturnsFrameId_AndLoadRoundTripsPixelIdentical()
    {
        using var frame = CreateFrame(48, 64, new Scalar(22, 44, 66));

        var frameId = _archive.StoreKeyFrame(frame, "match-1", snapshotVersion: 1);

        frameId.Should().NotBeNullOrWhiteSpace();
        _archive.IsKeyFrame("match-1", frameId).Should().BeTrue();

        using var loaded = _archive.LoadKeyFrame("match-1", frameId);

        loaded.Empty().Should().BeFalse();
        loaded.Rows.Should().Be(48);
        loaded.Cols.Should().Be(64);
        AreSamePixels(frame, loaded).Should().BeTrue();
    }

    [Fact]
    public void ShouldPersist_WhenOnlyVersionChanges_ReturnsFalse()
    {
        var prev = CreateSnapshot(gold: 10);

        var next = prev with { Version = prev.Version + 1 };

        _archive.ShouldPersist(prev, next).Should().BeFalse();
    }

    [Fact]
    public void ShouldPersist_WhenGoldChanges_ReturnsTrue()
    {
        var prev = CreateSnapshot(gold: 10);

        var next = prev with { Gold = 11 };

        _archive.ShouldPersist(prev, next).Should().BeTrue();
    }

    [Fact]
    public void ShouldPersist_WhenBoardUnitsChange_ReturnsTrue()
    {
        var prev = CreateSnapshot(boardUnits: new[] { new BoardUnitState(0, "Garen", 2, 1, Array.Empty<string>()) });
        var next = CreateSnapshot(boardUnits: new[] { new BoardUnitState(0, "Garen", 2, 2, Array.Empty<string>()) });

        _archive.ShouldPersist(prev, next).Should().BeTrue();
    }

    [Fact]
    public void StoreKeyFrame_SameFrameTwice_IsIdempotent_OnlyOneFile()
    {
        using var frame = CreateFrame(32, 32, new Scalar(10, 20, 30));

        var first = _archive.StoreKeyFrame(frame, "match-1", snapshotVersion: 1);
        var second = _archive.StoreKeyFrame(frame, "match-1", snapshotVersion: 2);

        first.Should().Be(second);
        _archive.ListKeyFrames("match-1").Should().HaveCount(1);
        Directory.GetFiles(MatchDirectory("match-1"), "*.png").Should().HaveCount(1);
    }

    [Fact]
    public void PruneOldMatches_Keep2_DeletesEarliestMatchDirectory()
    {
        using var frame = CreateFrame(16, 16, new Scalar(1, 2, 3));
        _archive.StoreKeyFrame(frame, "match-A", snapshotVersion: 1);
        _archive.StoreKeyFrame(frame, "match-B", snapshotVersion: 1);
        _archive.StoreKeyFrame(frame, "match-C", snapshotVersion: 1);

        SetMatchLastWrite("match-A", DateTime.UtcNow.AddHours(-3));
        SetMatchLastWrite("match-B", DateTime.UtcNow.AddHours(-2));
        SetMatchLastWrite("match-C", DateTime.UtcNow.AddHours(-1));

        _archive.PruneOldMatches(keepLatestN: 2);

        MatchDirectoryExists("match-A").Should().BeFalse();
        MatchDirectoryExists("match-B").Should().BeTrue();
        MatchDirectoryExists("match-C").Should().BeTrue();
    }

    [Fact]
    public void PruneOldMatches_LockedMatch_IsNotDeleted()
    {
        using var frame = CreateFrame(16, 16, new Scalar(1, 2, 3));
        _archive.StoreKeyFrame(frame, "match-old", snapshotVersion: 1);
        _archive.StoreKeyFrame(frame, "match-new", snapshotVersion: 1);

        SetMatchLastWrite("match-old", DateTime.UtcNow.AddHours(-3));
        SetMatchLastWrite("match-new", DateTime.UtcNow.AddHours(-1));

        _archive.Lock("match-old");
        _archive.PruneOldMatches(keepLatestN: 1);

        MatchDirectoryExists("match-old").Should().BeTrue();
        MatchDirectoryExists("match-new").Should().BeTrue();
    }

    [Fact]
    public void ListKeyFrames_ReturnsSortedBySnapshotVersionAscending()
    {
        using var f3 = CreateFrame(16, 16, new Scalar(3, 0, 0));
        using var f2 = CreateFrame(16, 16, new Scalar(2, 0, 0));
        using var f1 = CreateFrame(16, 16, new Scalar(1, 0, 0));

        _archive.StoreKeyFrame(f3, "match-1", snapshotVersion: 30);
        _archive.StoreKeyFrame(f2, "match-1", snapshotVersion: 10);
        _archive.StoreKeyFrame(f1, "match-1", snapshotVersion: 20);

        var keys = _archive.ListKeyFrames("match-1");

        keys.Select(key => key.SnapshotVersion).Should().Equal(10L, 20L, 30L);
        keys.Should().HaveCount(3);
    }

    [Fact]
    public void LoadKeyFrameWithMeta_RoundTripsLayoutMetadata()
    {
        using var frame = CreateFrame(48, 64, new Scalar(5, 6, 7));
        var layoutMeta = new Dictionary<string, string>
        {
            ["resolution"] = "1920x1080",
            ["layoutVersion"] = "1.3"
        };

        var frameId = _archive.StoreKeyFrame(frame, "match-1", snapshotVersion: 7, layoutMeta);

        var (loaded, meta) = _archive.LoadKeyFrameWithMeta("match-1", frameId);
        using (loaded)
        {
            meta.Should().NotBeNull();
            meta!["resolution"].Should().Be("1920x1080");
            meta["layoutVersion"].Should().Be("1.3");
            meta["width"].Should().Be("64");
            meta["height"].Should().Be("48");
            loaded.Empty().Should().BeFalse();
        }
    }

    private string MatchDirectory(string matchId) => Path.Combine(_tempRoot, "frames", matchId);

    private void SetMatchLastWrite(string matchId, DateTime utc)
        => Directory.SetLastWriteTimeUtc(MatchDirectory(matchId), utc);

    private bool MatchDirectoryExists(string matchId) => Directory.Exists(MatchDirectory(matchId));

    private static Mat CreateFrame(int height, int width, Scalar color)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3);
        mat.SetTo(color);
        return mat;
    }

    private static bool AreSamePixels(Mat left, Mat right)
    {
        return Cv2.Norm(left, right, NormTypes.L2) == 0.0;
    }

    private static GameStateSnapshot CreateSnapshot(
        int gold = 10,
        IReadOnlyList<BoardUnitState>? boardUnits = null,
        IReadOnlyList<BoardUnitState>? benchUnits = null,
        IReadOnlyList<ShopCardState>? shopCards = null)
    {
        return new GameStateSnapshot(
            Stage: "2-1",
            Phase: GamePhase.Planning,
            Gold: gold,
            Level: 7,
            Exp: 42,
            Hp: 100,
            Streak: 3,
            PlayerName: null,
            BoardUnits: boardUnits ?? Array.Empty<BoardUnitState>(),
            BenchUnits: benchUnits ?? Array.Empty<BoardUnitState>(),
            ShopCards: shopCards ?? Array.Empty<ShopCardState>(),
            Opponents: Array.Empty<OpponentSnapshot>(),
            Version: 1);
    }
}