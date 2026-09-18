using ChanSight.Core.MetaInfo;
using FluentAssertions;
using System.Text.Json;

namespace ChanSight.Tests.MetaInfo;

public sealed class MetaInfoStoreTests : IDisposable
{
    private readonly string _directory;
    private bool _disposed;

    public MetaInfoStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ChanSight.MetaInfoStore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
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
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string WriteJson(string fileName, object value)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(value, MetaJson.Options));
        return path;
    }

    private static MetaSource Source(double confidence = 0.9)
    {
        return new MetaSource { Type = MetaSourceType.Manual, Provider = "curator", Patch = "14.18", Confidence = confidence };
    }

    private static CompMeta Comp(string id, string name, params string[] units)
    {
        return new CompMeta
        {
            Id = id,
            Name = name,
            CompCode = $"CODE-{id}",
            IntrinsicRequirements = new IntrinsicRequirements(),
            Units = units.Select(u => new UnitBuild { HeroName = u }).ToList(),
            Source = Source(),
        };
    }

    [Fact]
    public void Load_ValidDirectory_LoadsAllFilesWithCorrectCounts()
    {
        WriteJson("comps.json", new[]
        {
            Comp("a", "Comp A", "Caitlyn", "Vi"),
            Comp("b", "Comp B", "Jarvan IV"),
        });
        WriteJson("version.json", new VersionMeta
        {
            Patch = "14.18",
            Tiers =
            [
                new TierEntry { CompId = "a", Tier = CompTier.T1, Source = Source() },
                new TierEntry { CompId = "b", Tier = CompTier.T0, Source = Source() },
            ],
            Source = Source(),
        });
        WriteJson("relations.json", new[]
        {
            new CompRelation { FromCompId = "a", ToCompId = "b", Kind = RelationKind.Counter, Source = Source() },
        });
        WriteJson("tips.json", new[]
        {
            new Tip { Id = "t1", CompId = "a", Content = "hold pairs", Confidence = 0.8, Source = Source() },
        });

        var store = new MetaInfoStore(_directory);

        store.Comps.Should().HaveCount(2);
        store.Version.Should().NotBeNull();
        store.Version!.Patch.Should().Be("14.18");
        store.Relations.Should().HaveCount(1);
        store.Tips.Should().HaveCount(1);
        store.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Load_DuplicateIdRowOutOfRangeDanglingRelationAndConfidence_AreRecordedAsIssues()
    {
        WriteJson("comps.json", new[]
        {
            Comp("a", "Comp A", "Caitlyn"),
            Comp("a", "Duplicate A", "Vi"),
            Comp("b", "Comp B", "Jarvan IV"),
        });
        WriteJson("version.json", new VersionMeta
        {
            Patch = "14.18",
            Tiers =
            [
                new TierEntry { CompId = "missing", Tier = CompTier.T1, Source = Source() },
            ],
            Source = Source(),
        });
        WriteJson("relations.json", new[]
        {
            new CompRelation { FromCompId = "a", ToCompId = "ghost", Kind = RelationKind.Flex, Source = Source() },
        });

        File.WriteAllText(Path.Combine(_directory, "bad-layout.json"), string.Empty);

        var store = new MetaInfoStore(_directory);

        store.Issues.Should().Contain(i => i.Contains("Duplicate CompMeta.Id 'a'"));
        store.Comps.Should().HaveCount(2); // duplicate "a" kept exactly once
    }

    [Fact]
    public void Load_OutOfRangeBoardPlacement_RecordsIssueAndDoesNotCrash()
    {
        const string compsJson = """
        [
          {
            "id": "a",
            "name": "Comp A",
            "compCode": "CODE-a",
            "intrinsicRequirements": {},
            "units": [ { "heroName": "Caitlyn" } ],
            "boardLayout": {
              "placements": [
                { "heroName": "Caitlyn", "row": 0, "col": 0 },
                { "heroName": "Vi", "row": 4, "col": 0 },
                { "heroName": "Jarvan IV", "row": 0, "col": 7 },
                { "heroName": "", "row": 1, "col": 1 }
              ]
            },
            "source": { "type": "Manual", "provider": "curator", "patch": "14.18", "confidence": 0.9 }
          }
        ]
        """;
        File.WriteAllText(Path.Combine(_directory, "comps.json"), compsJson);

        var store = new MetaInfoStore(_directory);

        store.Issues.Should().Contain(i => i.Contains("Row out of range"));
        store.Issues.Should().Contain(i => i.Contains("Col out of range"));
        store.Issues.Should().Contain(i => i.Contains("empty HeroName"));
        store.Comps.Should().HaveCount(1);
        store.Comps[0].BoardLayout!.Placements.Should().HaveCount(1);
    }

    [Fact]
    public void Load_ConfidenceOutOfRange_RecordsIssue()
    {
        WriteJson("comps.json", new[]
        {
            new CompMeta
            {
                Id = "a",
                Name = "Comp A",
                CompCode = "CODE-a",
                IntrinsicRequirements = new IntrinsicRequirements(),
                Source = Source(1.5),
            },
        });

        var store = new MetaInfoStore(_directory);

        store.Issues.Should().Contain(i => i.Contains("Confidence out of range"));
    }

    [Fact]
    public void QueryByUnits_OrdersByHitCountDescending()
    {
        WriteJson("comps.json", new[]
        {
            Comp("one-hit", "One Hit", "Caitlyn", "Seraphine"),
            Comp("two-hit", "Two Hits", "Caitlyn", "Vi", "Xayah"),
            Comp("zero-hit", "Zero Hits", "Jarvan IV"),
        });

        var store = new MetaInfoStore(_directory);

        var results = store.QueryByUnits(new[] { "Caitlyn", "Vi" });

        results.Select(c => c.Id).Should().Equal("two-hit", "one-hit");
    }

    [Fact]
    public void QueryByUnits_NoHits_ReturnsEmpty()
    {
        WriteJson("comps.json", new[]
        {
            Comp("a", "Comp A", "Caitlyn"),
        });

        var store = new MetaInfoStore(_directory);

        store.QueryByUnits(new[] { "Jarvan IV" }).Should().BeEmpty();
        store.QueryByUnits(Array.Empty<string>()).Should().BeEmpty();
    }

    [Fact]
    public void QueryTiers_ReturnsVersionTiers()
    {
        WriteJson("version.json", new VersionMeta
        {
            Patch = "14.18",
            Tiers =
            [
                new TierEntry { CompId = "a", Tier = CompTier.T0, Source = Source() },
            ],
            Source = Source(),
        });

        var store = new MetaInfoStore(_directory);

        store.QueryTiers().Should().HaveCount(1);
        store.QueryTiers()[0].CompId.Should().Be("a");
    }

    [Fact]
    public void RelationsOf_ReturnsRelationsForCompOnly()
    {
        WriteJson("comps.json", new[]
        {
            Comp("a", "Comp A"),
            Comp("b", "Comp B"),
            Comp("c", "Comp C"),
        });
        WriteJson("relations.json", new[]
        {
            new CompRelation { FromCompId = "a", ToCompId = "b", Kind = RelationKind.Counter, Source = Source() },
            new CompRelation { FromCompId = "c", ToCompId = "a", Kind = RelationKind.Flex, Source = Source() },
            new CompRelation { FromCompId = "b", ToCompId = "c", Kind = RelationKind.Upgrade, Source = Source() },
        });

        var store = new MetaInfoStore(_directory);

        var relations = store.RelationsOf("a");

        relations.Should().HaveCount(2);
        relations.Should().OnlyContain(r => r.FromCompId == "a" || r.ToCompId == "a");
    }

    [Fact]
    public void CompCodeOf_ReturnsCodeWhenCompExists()
    {
        WriteJson("comps.json", new[]
        {
            Comp("a", "Comp A"),
        });

        var store = new MetaInfoStore(_directory);

        store.CompCodeOf("a").Should().Be("CODE-a");
        store.CompCodeOf("missing").Should().BeNull();
    }

    [Fact]
    public void Load_EmptyDirectory_DoesNotThrow()
    {
        var store = new MetaInfoStore(_directory);

        store.Comps.Should().BeEmpty();
        store.Version.Should().BeNull();
        store.Relations.Should().BeEmpty();
        store.Tips.Should().BeEmpty();
        store.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Load_BadJsonFile_RecordsIssueAndLoadsRemainingFiles()
    {
        File.WriteAllText(Path.Combine(_directory, "relations.json"), "{ not valid json ");
        WriteJson("comps.json", new[]
        {
            Comp("a", "Comp A"),
        });
        WriteJson("tips.json", new[]
        {
            new Tip { Id = "t1", Content = "tip", Confidence = 0.5, Source = Source() },
        });

        var store = new MetaInfoStore(_directory);

        store.Comps.Should().HaveCount(1);
        store.Tips.Should().HaveCount(1);
        store.Relations.Should().BeEmpty();
        store.Issues.Should().Contain(i => i.Contains("Failed to parse relations.json"));
    }
}