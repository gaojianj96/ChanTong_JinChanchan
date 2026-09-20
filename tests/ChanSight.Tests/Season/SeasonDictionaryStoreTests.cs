using System.Text.Json;
using ChanSight.Core.Season;
using FluentAssertions;

namespace ChanSight.Tests.Season;

public sealed class SeasonDictionaryStoreTests : IDisposable
{
    private const string SeasonId = "S16.5";
    private static readonly DateTimeOffset Seen = new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);

    private readonly string _rootDirectory;
    private bool _disposed;

    public SeasonDictionaryStoreTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "ChanSight.Season", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void EmptyStore_Get_ReturnsNull_DoesNotThrow()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        var result = store.Get(SeasonId);

        result.Should().BeNull();
    }

    [Fact]
    public void AddCandidate_PendingContainsCandidate_FormalDoesNot()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);
        var candidate = NewCandidate("新英雄", "hero");

        store.AddCandidate(SeasonId, candidate);

        store.GetPendingCandidates(SeasonId).Should().ContainSingle(c => c.Entity == candidate.Entity);
        store.Get(SeasonId).Should().BeNull();
    }

    [Fact]
    public void ConfirmNew_FormalContainsEntity_CandidateRemoved()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("新英雄", "hero"));
        store.Confirm(SeasonId, "新英雄", ConfirmKind.New);

        store.GetPendingCandidates(SeasonId).Should().BeEmpty();
        store.Get(SeasonId)!.Heroes.Should().Contain("新英雄");
    }

    [Fact]
    public void ConfirmIgnore_CandidateRemoved_FormalDoesNotContain()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("噪声条目", "hero"));
        store.Confirm(SeasonId, "噪声条目", ConfirmKind.Ignore);

        store.GetPendingCandidates(SeasonId).Should().BeEmpty();
        store.Get(SeasonId).Should().BeNull();
    }

    [Fact]
    public void ConfirmMapExisting_CandidateRemoved_NoFormalAddition()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("盖伦别名", "hero"));
        store.Confirm(SeasonId, "盖伦别名", ConfirmKind.MapExisting);

        store.GetPendingCandidates(SeasonId).Should().BeEmpty();
        store.Get(SeasonId).Should().BeNull();
    }

    [Fact]
    public void Persistence_ReloadSameDirectory_RoundTripsFormalAndCandidates()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("新英雄", "hero"));
        store.Confirm(SeasonId, "新英雄", ConfirmKind.New);

        store.AddCandidate(SeasonId, NewCandidate("新装备", "item"));
        store.Confirm(SeasonId, "新装备", ConfirmKind.New);

        store.AddCandidate(SeasonId, NewCandidate("待确认英雄", "hero"));

        var reloaded = new SeasonDictionaryStore(_rootDirectory);

        reloaded.Get(SeasonId)!.Heroes.Should().Contain("新英雄");
        reloaded.Get(SeasonId)!.Items.Should().Contain("新装备");
        reloaded.GetPendingCandidates(SeasonId).Should().ContainSingle(c => c.Entity == "待确认英雄");

        var reloadedCandidate = reloaded.GetPendingCandidates(SeasonId)[0];
        reloadedCandidate.Source.Should().Be("hero");
        reloadedCandidate.Confidence.Should().Be(0.9);
        reloadedCandidate.FirstSeen.Should().Be(Seen);
    }

    [Fact]
    public void Revoke_RemovesFormalEntity()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("新英雄", "hero"));
        store.Confirm(SeasonId, "新英雄", ConfirmKind.New);
        store.Revoke(SeasonId, "新英雄");

        store.Get(SeasonId).Should().BeNull();
    }

    [Fact]
    public void Reader_TryGetHeroAndItem_CaseInsensitive()
    {
        var store = new SeasonDictionaryStore(_rootDirectory);

        store.AddCandidate(SeasonId, NewCandidate("盖伦", "hero"));
        store.Confirm(SeasonId, "盖伦", ConfirmKind.New);

        store.TryGetHero(SeasonId, "盖伦").Should().BeTrue();
        store.TryGetHero(SeasonId, "garen").Should().BeFalse();
        store.TryGetItem(SeasonId, "盖伦").Should().BeFalse();
    }

    [Fact]
    public void Registry_BuiltinDefaultMapping_UnknownReturnsNull()
    {
        var registry = new SeasonRegistry();

        registry.MapSeasonToPatch("S16.5").Should().Be("9.11-9.16");
        registry.MapSeasonToPatch("S99").Should().BeNull();
    }

    [Fact]
    public void Registry_LoadJson_MergesMappings()
    {
        var registryPath = Path.Combine(_rootDirectory, "registry.json");
        File.WriteAllText(registryPath, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["S17"] = "9.17-9.22",
        }));

        var registry = new SeasonRegistry(registryPath);

        registry.MapSeasonToPatch("S17").Should().Be("9.17-9.22");
        registry.MapSeasonToPatch("S16.5").Should().Be("9.11-9.16");
    }

    private static CandidateMeta NewCandidate(string entity, string source)
        => new(entity, source, null, 0.9, Seen);
}