using ChanSight.Core.MetaInfo;
using ChanSight.Core.Season;
using ChanSight.Overlay.ViewModels;
using FluentAssertions;

namespace ChanSight.Tests.Overlay;

/// <summary>
/// 契约 DICT-VIEW 的验收测试: 字典查看 ViewModel 的三集合填充、候选读取、梯度映射、
/// 空 meta 容错与 prompt 文本。
/// </summary>
public sealed class DictionaryViewModelTests
{
    [Fact]
    public void Runtime_FillsHeroesItemsTraitsCollections()
    {
        var runtime = Runtime(Dict(
            "S16.5",
            heroes: ["英雄甲", "英雄乙"],
            items: ["羊刀", "蓝霸符"],
            traits: ["法师", "狙神"]));
        var store = new FakeStore();

        var vm = new DictionaryViewModel(runtime, store);

        vm.Heroes.Should().BeEquivalentTo("英雄甲", "英雄乙");
        vm.Items.Should().BeEquivalentTo("羊刀", "蓝霸符");
        vm.Traits.Should().BeEquivalentTo("法师", "狙神");
    }

    [Fact]
    public void Store_ReadsPendingCandidatesCorrectly()
    {
        var runtime = Runtime(Dict("S18"));
        var store = new FakeStore(
            new CandidateMeta("悠米", "hero", null, 0.9, DateTimeOffset.UtcNow),
            new CandidateMeta("泽丽", "hero", null, 0.8, DateTimeOffset.UtcNow));

        var vm = new DictionaryViewModel(runtime, store);

        vm.PendingCandidates.Should().BeEquivalentTo("悠米", "泽丽");
    }

    [Fact]
    public void CompDisplay_TierText_MapsTierToChineseLabel()
    {
        CompDisplay.TierText(CompTier.T0).Should().Be("S+");
        CompDisplay.TierText(CompTier.T1).Should().Be("S");
        CompDisplay.TierText(CompTier.T2).Should().Be("S-");
    }

    [Fact]
    public void EmptyMetaInfo_CompsEmpty_StatusMentionsNoData_DoesNotThrow()
    {
        using var fx = new MetaDirectoryFixture();
        var runtime = Runtime(Dict("S16.5"));

        // metaRootDirectory 指向空目录(无 comps.json/version.json) → Comps 空、状态含「暂无」。
        var vm = new DictionaryViewModel(
            runtime,
            new FakeStore(),
            buildPrompt: () => "prompt",
            metaRootDirectory: fx.Root);

        vm.Comps.Should().BeEmpty();
        vm.HasNoComps.Should().BeTrue();
        vm.Status.Should().Contain("暂无");
        vm.Status.Should().Contain("待导入");
    }

    [Fact]
    public void BuildPrompt_Source_TextIsNonEmpty()
    {
        const string prompt = "你是《金铲铲之战》整帧识别助手。…";
        var runtime = Runtime(Dict("S16.5"));

        var vm = new DictionaryViewModel(
            runtime,
            new FakeStore(),
            buildPrompt: () => prompt);

        vm.VlmPrompt.Should().NotBeNullOrWhiteSpace();
        vm.VlmPrompt.Should().Be(prompt);
    }

    private static SeasonRuntime Runtime(SeasonDictionary dictionary) => new(new FakeReader(dictionary));

    private static SeasonDictionary Dict(
        string seasonId,
        string[]? heroes = null,
        string[]? items = null,
        string[]? traits = null)
        => new(
            seasonId,
            new HashSet<string>(heroes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(items ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(traits ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase));

    private sealed class FakeReader : ISeasonDictionaryReader
    {
        private readonly SeasonDictionary _dictionary;

        public FakeReader(SeasonDictionary dictionary) => _dictionary = dictionary;

        public SeasonDictionary? Get(string seasonId) => _dictionary;

        public bool TryGetHero(string seasonId, string name) => _dictionary.Heroes.Contains(name);

        public bool TryGetItem(string seasonId, string name) => _dictionary.Items.Contains(name);
    }

    private sealed class FakeStore : ISeasonDictionaryWriter
    {
        private readonly IReadOnlyList<CandidateMeta> _candidates;

        public FakeStore(params CandidateMeta[] candidates) => _candidates = candidates;

        public void AddCandidate(string seasonId, CandidateMeta candidate)
        {
        }

        public void Confirm(string seasonId, string entity, ConfirmKind kind)
        {
        }

        public void Revoke(string seasonId, string entity)
        {
        }

        public IReadOnlyList<CandidateMeta> GetPendingCandidates(string seasonId) => _candidates;
    }

    private sealed class MetaDirectoryFixture : IDisposable
    {
        private bool _disposed;

        public MetaDirectoryFixture() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ChanSight.DictView", Guid.NewGuid().ToString("N"));

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