using ChanSight.Core.MetaInfo;
using FluentAssertions;

namespace ChanSight.Tests.MetaInfo;

public sealed class MetaSeedTests
{
    // S+ comps (must map to T0).
    private static readonly string[] SPlusIds = ["diyu-huo-lunzi", "qi-ye-flex", "lie-long-95", "heian-yishi-zhizhu"];

    // Comps deliberately without a source URL ("(无链接)" in comp_code_table.md).
    private static readonly string[] NoLinkIds = ["heian-yishi-zhizhu", "5ge-3xing-yaoji", "xianji-liu-shahuang", "dengdai-tianhu-xiaolan"];

    private readonly MetaInfoStore _store;

    public MetaSeedTests()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "data", "meta", "S18");
        _store = new MetaInfoStore(directory);
    }

    [Fact]
    public void Load_SeedComps_HasAtLeast25CompsWithoutIssues()
    {
        _store.Comps.Should().HaveCountGreaterThanOrEqualTo(25);
        _store.Issues.Should().BeEmpty();
    }

    [Fact]
    public void EveryComp_HasNonEmptyNameCodeAndMatchingTierEntry()
    {
        var tiersById = _store.QueryTiers().ToDictionary(t => t.CompId, StringComparer.Ordinal);

        tiersById.Should().HaveSameCount(_store.Comps);

        foreach (var comp in _store.Comps)
        {
            comp.Name.Should().NotBeNullOrWhiteSpace($"comp '{comp.Id}' should have a Name");
            comp.CompCode.Should().NotBeNullOrWhiteSpace($"comp '{comp.Id}' should have a CompCode");
            tiersById.Should().ContainKey(comp.Id, $"comp '{comp.Id}' should have a TierEntry");
        }
    }

    [Fact]
    public void SPlusComps_MapToT0()
    {
        var tiersById = _store.QueryTiers().ToDictionary(t => t.CompId, StringComparer.Ordinal);

        foreach (var id in SPlusIds)
        {
            tiersById[id].Tier.Should().Be(CompTier.T0, $"'{id}' should map S+ -> T0");
        }
    }

    [Fact]
    public void TierBuckets_MatchSourceGradients()
    {
        var tiers = _store.QueryTiers();

        tiers.Count(t => t.Tier == CompTier.T0).Should().Be(4); // S+
        tiers.Count(t => t.Tier == CompTier.T1).Should().Be(11); // S
        tiers.Count(t => t.Tier == CompTier.T2).Should().Be(13); // S-
    }

    [Fact]
    public void LinkedComps_HaveUrl_NoLinkComps_HaveNullUrl()
    {
        var byId = _store.Comps.ToDictionary(c => c.Id, StringComparer.Ordinal);

        foreach (var id in NoLinkIds)
        {
            byId[id].Source.Url.Should().BeNull($"'{id}' has no link");
        }

        _store.Comps
            .Where(c => !NoLinkIds.Contains(c.Id))
            .Should().NotBeEmpty()
            .And.OnlyContain(c => !string.IsNullOrWhiteSpace(c.Source.Url));
    }

    [Fact]
    public void QueryTiers_CountMatchesCompsCount()
    {
        _store.QueryTiers().Should().HaveCount(_store.Comps.Count);
    }
}