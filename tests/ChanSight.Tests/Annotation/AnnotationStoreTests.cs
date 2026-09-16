using System.Text.Json;
using ChanSight.Core.Annotation;
using FluentAssertions;

namespace ChanSight.Tests.Annotation;

public sealed class AnnotationStoreTests
{
    private const string BaseDir = "corrections";
    private const string MatchId = "m1";

    [Fact]
    public async Task AppendCorrection_ThenLoad_RoundTripsAllFields()
    {
        var fs = new FakeFileSystem();
        var store = new AnnotationStore(fs, BaseDir);
        var expected = new CorrectionRecord(
            "c1", MatchId, "f1",
            new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
            42,
            RegionTypes.BoardHero,
            3,
            "盖伦",
            "{\"hero\":\"盖伦\",\"star\":2}",
            0.87,
            "local",
            "v1");

        await store.AppendCorrectionAsync(expected);

        var loaded = await store.LoadCorrectionsAsync(MatchId);

        loaded.Should().ContainSingle();
        loaded[0].Should().BeEquivalentTo(expected);
        loaded[0].SchemaVersion.Should().Be("1");
        loaded[0].RecognizedValue.Should().Be("盖伦");
    }

    [Fact]
    public async Task GoldLabelRevocation_LoadFiltersRevoked_IncludingRevokedReturnsBoth()
    {
        var fs = new FakeFileSystem();
        var store = new AnnotationStore(fs, BaseDir);
        const string id = "g1";

        await store.AppendGoldLabelAsync(NewGoldLabel(id, revoked: false));
        await store.AppendGoldLabelAsync(NewGoldLabel(id, revoked: true));

        var filtered = await store.LoadGoldLabelsAsync(MatchId);
        var all = await store.LoadGoldLabelsIncludingRevokedAsync(MatchId);

        filtered.Should().BeEmpty();
        all.Should().HaveCount(2);
        all[0].Revoked.Should().BeFalse();
        all[1].Revoked.Should().BeTrue();
    }

    [Fact]
    public async Task GoldLabelUnRevocation_LatestWins_RestoresLabel()
    {
        var fs = new FakeFileSystem();
        var store = new AnnotationStore(fs, BaseDir);
        const string id = "g1";

        await store.AppendGoldLabelAsync(NewGoldLabel(id, revoked: false));
        await store.AppendGoldLabelAsync(NewGoldLabel(id, revoked: true));
        await store.AppendGoldLabelAsync(NewGoldLabel(id, revoked: false));

        var filtered = await store.LoadGoldLabelsAsync(MatchId);

        filtered.Should().ContainSingle();
        filtered[0].Revoked.Should().BeFalse();
    }

    [Fact]
    public async Task CorrectionRecord_StructuredCorrectedValue_RoundTrips()
    {
        var fs = new FakeFileSystem();
        var store = new AnnotationStore(fs, BaseDir);
        var withItems = NewCorrection(
            "c1",
            RegionTypes.BoardItems,
            "{\"hero\":\"盖伦\",\"star\":2,\"items\":[\"羊刀\"]}");
        var empty = NewCorrection("c2", RegionTypes.BoardItems, "{\"empty\":true}", recognized: null);

        await store.AppendCorrectionAsync(withItems);
        await store.AppendCorrectionAsync(empty);

        var loaded = await store.LoadCorrectionsAsync(MatchId);

        loaded.Should().HaveCount(2);
        loaded[0].CorrectedValue.Should().Be("{\"hero\":\"盖伦\",\"star\":2,\"items\":[\"羊刀\"]}");
        loaded[1].CorrectedValue.Should().Be("{\"empty\":true}");
        loaded[1].RecognizedValue.Should().BeNull();

        using (var doc = JsonDocument.Parse(loaded[0].CorrectedValue))
        {
            doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        }

        using (var doc = JsonDocument.Parse(loaded[1].CorrectedValue))
        {
            doc.RootElement.GetProperty("empty").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task LoadCorrections_InvalidJsonLine_SkipsAndReportsIssue()
    {
        var fs = new FakeFileSystem();
        var store = new AnnotationStore(fs, BaseDir);

        await store.AppendCorrectionAsync(NewCorrection("c1"));
        fs.AppendRaw(CorrectionsPath(MatchId), "{ not json");

        var detailed = await store.LoadCorrectionsWithIssuesAsync(MatchId);
        var plain = await store.LoadCorrectionsAsync(MatchId);

        detailed.Records.Should().ContainSingle();
        detailed.Issues.Should().ContainSingle(i => i.Contains("not valid JSON", StringComparison.Ordinal));
        plain.Should().ContainSingle();
    }

    [Fact]
    public async Task AppendCorrection_ConcurrentTasks_NoLostLines()
    {
        var fs = new FakeFileSystem();
        var store = new AnnotationStore(fs, BaseDir);
        const int count = 200;

        await Task.WhenAll(Enumerable.Range(0, count).Select(i => store.AppendCorrectionAsync(NewCorrection($"c{i}"))));

        var loaded = await store.LoadCorrectionsAsync(MatchId);

        loaded.Should().HaveCount(count);
        loaded.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal)
            .Should().Equal(
                Enumerable.Range(0, count).Select(i => $"c{i}").OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AppendCorrection_InvalidRegionType_Throws()
    {
        var store = new AnnotationStore(new FakeFileSystem(), BaseDir);
        var bad = NewCorrection("c1") with { RegionType = "board.invalid" };

        var act = async () => await store.AppendCorrectionAsync(bad);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AppendGoldLabel_InvalidCorrectionType_Throws()
    {
        var store = new AnnotationStore(new FakeFileSystem(), BaseDir);
        var bad = NewGoldLabel("g1") with { CorrectionType = "nope" };

        var act = async () => await store.AppendGoldLabelAsync(bad);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void RegionTypes_AllConstants_AreValid()
    {
        RegionTypes.All.Should().HaveCount(9);
        RegionTypes.All.Should().OnlyContain(value => RegionTypes.IsValid(value));
        RegionTypes.IsValid("board.invalid").Should().BeFalse();
        RegionTypes.IsValid(null).Should().BeFalse();
    }

    [Fact]
    public void CorrectionTypes_AllConstants_AreValid()
    {
        CorrectionTypes.All.Should().HaveCount(5);
        CorrectionTypes.All.Should().OnlyContain(value => CorrectionTypes.IsValid(value));
        CorrectionTypes.IsValid(null).Should().BeTrue();
        CorrectionTypes.IsValid("nope").Should().BeFalse();
    }

    private static CorrectionRecord NewCorrection(
        string id,
        string regionType = RegionTypes.BoardHero,
        string correctedValue = "{\"hero\":\"盖伦\"}",
        string? recognized = "盖伦")
    {
        return new CorrectionRecord(
            id, MatchId, "f1",
            new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
            1,
            regionType,
            0,
            recognized,
            correctedValue,
            0.9,
            "local",
            "v1");
    }

    private static GoldLabel NewGoldLabel(string id, bool revoked = false)
    {
        return new GoldLabel(
            id,
            CorrectionId: "corr-" + id,
            MatchId,
            "f1",
            1,
            RegionTypes.BoardHero,
            0,
            "盖伦",
            "{\"hero\":\"盖伦\"}",
            0.9,
            "local",
            "v1",
            CorrectionTypes.Classification,
            revoked,
            ConfirmedAt: new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero));
    }

    private static string CorrectionsPath(string matchId) => Path.Combine(BaseDir, matchId, "corrections.jsonl");

    private sealed class FakeFileSystem : IFileSystem
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public void CreateDirectory(string path)
        {
        }

        public Task AppendLineAsync(string path, string line, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _files[path] = _files.TryGetValue(path, out var existing) ? existing + line + "\n" : line + "\n";
            }

            return Task.CompletedTask;
        }

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult(_files.TryGetValue(path, out var text) ? (string?)text : null);
            }
        }

        public void AppendRaw(string path, string line)
        {
            lock (_sync)
            {
                _files[path] = _files.TryGetValue(path, out var existing) ? existing + line + "\n" : line + "\n";
            }
        }
    }
}