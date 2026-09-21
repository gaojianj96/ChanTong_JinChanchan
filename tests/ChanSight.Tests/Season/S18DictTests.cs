using ChanSight.Core.Season;
using FluentAssertions;

namespace ChanSight.Tests.Season;

/// <summary>
/// 契约 S18-DICT 验收测试: S18(自然之力)种子字典覆盖阵容码表英雄/装备,
/// 且注册进 SeasonDictionaryStore 后 Get("S18") 返回与 S16.5 可区分的 S18 字典。
/// </summary>
public sealed class S18DictTests
{
    [Fact]
    public void Seed_HeroesItemsTraits_NonEmpty_ReasonableCount()
    {
        S18DictionarySeed.Seed.SeasonId.Should().Be("S18");
        S18DictionarySeed.Heroes.Should().NotBeEmpty();
        S18DictionarySeed.Heroes.Should().HaveCountGreaterThan(50);
        S18DictionarySeed.Items.Should().NotBeEmpty();
        S18DictionarySeed.Traits.Should().NotBeEmpty();
    }

    [Fact]
    public void Seed_Heroes_CoversCompCodeTableKeyHeroes()
    {
        S18DictionarySeed.Heroes.Should().Contain(
            "韦鲁斯", "螳螂", "女警", "月男", "沙皇", "卡蜜尔", "剑圣");
    }

    [Fact]
    public void Seed_Items_CoversKeyItems()
    {
        S18DictionarySeed.Items.Should().Contain(
            "青龙刀", "羊刀", "战刃", "大剑", "科技枪", "法爆");
    }

    [Fact]
    public void Store_RegisteredS18Seed_GetS18_ReturnsS18Dictionary_DistinctFromS16_5()
    {
        using var fx = new StoreFixture();
        var store = new SeasonDictionaryStore(fx.Root, BuiltInSeeds());

        var s18 = store.Get("S18");
        var s165 = store.Get("S16.5");

        s18.Should().NotBeNull();
        s165.Should().NotBeNull();
        s18!.Heroes.Should().Contain("韦鲁斯");
        s165!.Heroes.Should().NotContain("韦鲁斯");
        s18.Heroes.Should().NotBeEquivalentTo(s165.Heroes);
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
