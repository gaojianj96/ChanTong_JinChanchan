using ChanSight.Core.Season;
using FluentAssertions;

namespace ChanSight.Tests.Season;

public sealed class SeasonRuntimeTests
{
    [Fact]
    public void DefaultContext_IsS16_5CongratulationsMode()
    {
        var runtime = new SeasonRuntime(new EmptyReader());

        runtime.Context.SeasonId.Should().Be("S16.5");
        runtime.Context.Mode.Should().Be("恭喜发财");
    }

    [Fact]
    public void EmptyReader_FallsBackToSeedHeroesItemsTraits()
    {
        var runtime = new SeasonRuntime(new EmptyReader());

        runtime.Heroes.Should().Contain("亚索");
        runtime.Heroes.Should().Contain("阿狸");
        runtime.Heroes.Should().HaveCountGreaterOrEqualTo(90);
        runtime.Items.Should().Contain("无尽之刃");
        runtime.Traits.Should().Contain("法师");
        runtime.TryGetHero("亚索").Should().BeTrue();
        runtime.TryGetItem("无尽之刃").Should().BeTrue();
        runtime.TryGetHero("不存在的英雄").Should().BeFalse();
    }

    [Fact]
    public void Current_UsesReaderDictionary_WhenPresent_NoFallback()
    {
        var reader = new FakeReader(new SeasonDictionary(
            "S16.5",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "自定义英雄" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "自定义装备" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "自定义羁绊" }));
        var runtime = new SeasonRuntime(reader);

        runtime.Heroes.Should().BeEquivalentTo(new[] { "自定义英雄" });
        runtime.Heroes.Should().NotContain("亚索");
        runtime.TryGetHero("自定义英雄").Should().BeTrue();
    }

    [Fact]
    public void Select_SwitchesCurrentSeason()
    {
        var reader = new FakeReader(new SeasonDictionary(
            "S17",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "新赛季英雄" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        var runtime = new SeasonRuntime(reader);

        runtime.Select("S17", "标准模式");

        runtime.Context.SeasonId.Should().Be("S17");
        runtime.Context.Mode.Should().Be("标准模式");
        runtime.Heroes.Should().Contain("新赛季英雄");
    }

    [Fact]
    public void Store_BuiltInSeed_Get_ReturnsSeedHeroes()
    {
        using var fx = new StoreFixture();
        var store = new SeasonDictionaryStore(fx.Root, BuiltInSeeds());

        var dictionary = store.Get("S16.5");

        dictionary.Should().NotBeNull();
        dictionary!.Heroes.Should().Contain("亚索");
        dictionary.Heroes.Should().HaveCountGreaterOrEqualTo(90);
    }

    [Fact]
    public void Store_BuiltInSeed_ConfirmNew_KeepsSeedHeroes()
    {
        using var fx = new StoreFixture();
        var store = new SeasonDictionaryStore(fx.Root, BuiltInSeeds());

        store.AddCandidate("S16.5", new CandidateMeta("全新英雄", "hero", null, 0.9, DateTimeOffset.UtcNow));
        store.Confirm("S16.5", "全新英雄", ConfirmKind.New);

        var dictionary = store.Get("S16.5");
        dictionary!.Heroes.Should().Contain("亚索");
        dictionary.Heroes.Should().Contain("全新英雄");
    }

    private static IReadOnlyDictionary<string, SeasonDictionary> BuiltInSeeds() =>
        new Dictionary<string, SeasonDictionary>(StringComparer.Ordinal)
        {
            [SeasonDictionarySeed.DefaultSeasonId] = SeasonDictionarySeed.DefaultDictionary,
        };

    private sealed class EmptyReader : ISeasonDictionaryReader
    {
        public SeasonDictionary? Get(string seasonId) => null;

        public bool TryGetHero(string seasonId, string name) => false;

        public bool TryGetItem(string seasonId, string name) => false;
    }

    private sealed class FakeReader : ISeasonDictionaryReader
    {
        private readonly SeasonDictionary _dictionary;

        public FakeReader(SeasonDictionary dictionary) => _dictionary = dictionary;

        public SeasonDictionary? Get(string seasonId) => _dictionary;

        public bool TryGetHero(string seasonId, string name) => _dictionary.Heroes.Contains(name);

        public bool TryGetItem(string seasonId, string name) => _dictionary.Items.Contains(name);
    }

    private sealed class StoreFixture : IDisposable
    {
        private bool _disposed;

        public StoreFixture() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ChanSight.SeasonRuntime", Guid.NewGuid().ToString("N"));

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
