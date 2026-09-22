using ChanSight.Core.Season;
using FluentAssertions;

namespace ChanSight.Tests.Season;

/// <summary>
/// 契约 S18-DICT-V2 验收测试: S18(自然之力)种子字典为 topmeta 权威数据(65 英雄含费用 / 35 羁绊 / 装备),
/// 加费用字段后可经 SeasonDictionaryStore 往返落盘。
/// </summary>
public sealed class S18DictTests
{
    [Fact]
    public void Seed_HeroesCount_Is65_And_HeroCosts_CoversAllHeroes_Within1To5()
    {
        S18DictionarySeed.Heroes.Should().HaveCount(65);
        S18DictionarySeed.HeroCosts.Should().HaveCount(65);

        foreach (var hero in S18DictionarySeed.Heroes)
        {
            S18DictionarySeed.HeroCosts.Should().ContainKey(hero, $"hero '{hero}' should have a cost");
            S18DictionarySeed.HeroCosts[hero].Should().BeInRange(1, 5, $"hero '{hero}' cost should be 1-5");
        }
    }

    [Theory]
    [InlineData("拉克丝", 5)]
    [InlineData("韦鲁斯", 1)]
    [InlineData("卡兹克", 3)]
    [InlineData("厄斐琉斯", 4)]
    public void Seed_HeroCosts_SpotChecks(string hero, int expectedCost)
    {
        S18DictionarySeed.HeroCosts[hero].Should().Be(expectedCost);
    }

    [Fact]
    public void Seed_TraitsCount_Is35_And_ContainsJungleMonster()
    {
        S18DictionarySeed.Traits.Should().HaveCount(35);
        S18DictionarySeed.Traits.Should().Contain("峡谷野怪");
    }

    [Fact]
    public void Seed_Items_ContainFormalNames_NotSlang()
    {
        S18DictionarySeed.Items.Should().Contain("朔极之矛", "鬼索的狂暴之刃", "无尽之刃");
        S18DictionarySeed.Items.Should().NotContain("青龙刀");
        S18DictionarySeed.Items.Should().NotContain("羊刀");
    }

    [Fact]
    public void Seed_Aliases_MapSlangToFormalNames()
    {
        S18DictionarySeed.HeroAliases["轮子"].Should().Be("希维尔");
        S18DictionarySeed.HeroAliases["大嘴"].Should().Be("克格莫");
        S18DictionarySeed.HeroAliases["天使"].Should().Be("凯尔");
        S18DictionarySeed.ItemAliases["羊刀"].Should().Be("鬼索的狂暴之刃");
        S18DictionarySeed.ItemAliases["青龙刀"].Should().Be("朔极之矛");
        S18DictionarySeed.ItemAliases["天使"].Should().Be("大天使之杖");
    }

    [Fact]
    public void Store_LoadsS18DictionaryJson_WithHeroCosts()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "data", "season");
        var store = new SeasonDictionaryStore(root);

        var s18 = store.Get("S18");

        s18.Should().NotBeNull();
        s18!.Heroes.Should().HaveCount(65);
        s18.HeroCosts.Should().HaveCount(65);
        s18.HeroCosts["拉克丝"].Should().Be(5);
        s18.HeroCosts["韦鲁斯"].Should().Be(1);
    }

    [Fact]
    public void Store_HeroCosts_RoundTripsThroughDictionaryJson()
    {
        using var fx = new StoreFixture();

        var store1 = new SeasonDictionaryStore(fx.Root, BuiltInSeeds());
        store1.AddCandidate("S18", new CandidateMeta("全新英雄", "hero", null, 0.9, DateTimeOffset.UtcNow));
        store1.Confirm("S18", "全新英雄", ConfirmKind.New);

        var store2 = new SeasonDictionaryStore(fx.Root);

        var s18 = store2.Get("S18");

        s18.Should().NotBeNull();
        s18!.Heroes.Should().Contain("全新英雄");
        s18.HeroCosts.Should().HaveCount(65);
        s18.HeroCosts["拉克丝"].Should().Be(5);
        s18.HeroCosts["厄斐琉斯"].Should().Be(4);
    }

    private static IReadOnlyDictionary<string, SeasonDictionary> BuiltInSeeds() =>
        new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
        {
            [SeasonDictionarySeed.DefaultSeasonId] = SeasonDictionarySeed.DefaultDictionary,
            [S18DictionarySeed.SeasonId] = S18DictionarySeed.Seed,
        };

    private sealed class StoreFixture : IDisposable
    {
        private bool _disposed;

        public StoreFixture() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ChanSight.S18Dict", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
