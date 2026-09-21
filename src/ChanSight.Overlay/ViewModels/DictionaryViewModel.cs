using System.Collections.ObjectModel;
using ChanSight.Core.MetaInfo;
using ChanSight.Core.Season;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChanSight.Overlay.ViewModels;

/// <summary>
/// 推荐阵容展示项: 阵容名(中文)/梯度标签(S+ / S / S-)/阵容码/前置要求/升降档。
/// 由 <see cref="DictionaryViewModel"/> 从 <see cref="MetaInfoStore"/> 的 Comps + Tiers 拼装。
/// </summary>
public sealed class CompDisplay
{
    public CompDisplay(
        string name,
        CompTier? tier,
        string compCode,
        string requirements,
        string conditionals)
    {
        Name = name;
        TierLabel = tier.HasValue ? TierText(tier.Value) : "未分档";
        CompCode = compCode;
        Requirements = requirements;
        Conditionals = conditionals;
    }

    /// <summary>阵容名。</summary>
    public string Name { get; }

    /// <summary>梯度标签(S+ / S / S-)。</summary>
    public string TierLabel { get; }

    /// <summary>阵容码(供复制)。</summary>
    public string CompCode { get; }

    /// <summary>前置要求(含二星/转职/装备/强化/最低数量)。</summary>
    public string Requirements { get; }

    /// <summary>升降档条件说明。</summary>
    public string Conditionals { get; }

    /// <summary>CompTier → 中文梯度标签: T0→S+, T1→S, T2→S-。</summary>
    public static string TierText(CompTier tier) => tier switch
    {
        CompTier.T0 => "S+",
        CompTier.T1 => "S",
        CompTier.T2 => "S-",
        _ => "?",
    };
}

/// <summary>
/// 字典查看 ViewModel: 展示当前赛季的奕子/装备/羁绊列表、VLM prompt 文本、自学习候选、
/// 以及 MetaInfoStore 的推荐阵容(名/梯度/阵容码/前置/升降档), 并留出范例识别头像占位说明。
/// 视图打开或手动刷新时从 SeasonRuntime 重读字典, 并按 SeasonRegistry 映射加载对应 patch 的 meta 数据。
/// </summary>
public partial class DictionaryViewModel : ObservableObject
{
    private readonly SeasonRuntime _runtime;
    private readonly ISeasonDictionaryWriter _dictionaryStore;
    private readonly Func<string> _buildPrompt;
    private readonly SeasonRegistry _registry;
    private readonly string? _metaRootDirectory;

    public DictionaryViewModel(
        SeasonRuntime runtime,
        ISeasonDictionaryWriter dictionaryStore,
        Func<string>? buildPrompt = null,
        SeasonRegistry? registry = null,
        string? metaRootDirectory = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _dictionaryStore = dictionaryStore ?? throw new ArgumentNullException(nameof(dictionaryStore));
        _buildPrompt = buildPrompt ?? new Func<string>(static () => string.Empty);
        _registry = registry ?? new SeasonRegistry();
        _metaRootDirectory = metaRootDirectory;

        Refresh();
    }

    /// <summary>当前赛季奕子名列表(排序, 供标签流展示)。</summary>
    public ObservableCollection<string> Heroes { get; } = new();

    /// <summary>当前赛季装备列表(排序)。</summary>
    public ObservableCollection<string> Items { get; } = new();

    /// <summary>当前赛季羁绊列表(排序)。</summary>
    public ObservableCollection<string> Traits { get; } = new();

    /// <summary>自学习候选项名称列表。</summary>
    public ObservableCollection<string> PendingCandidates { get; } = new();

    /// <summary>推荐阵容展示项列表(空则无数据)。</summary>
    public ObservableCollection<CompDisplay> Comps { get; } = new();

    /// <summary>当前 season + mode 的完整 VLM prompt 文本。</summary>
    [ObservableProperty]
    private string _vlmPrompt = string.Empty;

    /// <summary>状态摘要(各区块数量, 无阵容数据时标注「暂无/待导入」)。</summary>
    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>是否无推荐阵容数据(控制「暂无」提示显示)。</summary>
    public bool HasNoComps => Comps.Count == 0;

    /// <summary>
    /// 重读当前 season 字典、候选、prompt 与推荐阵容。视图打开 / 切换赛季后调用(手动刷新)。
    /// </summary>
    [RelayCommand]
    public void Refresh()
    {
        var seasonId = _runtime.Context.SeasonId;

        RebuildDictionary(_runtime.Heroes, _runtime.Items, _runtime.Traits);
        RebuildCandidates(seasonId);
        RebuildComps(seasonId);

        VlmPrompt = _buildPrompt();

        Status = ComposeStatus();
        OnPropertyChanged(nameof(HasNoComps));
    }

    private void RebuildDictionary(
        IReadOnlySet<string> heroes,
        IReadOnlySet<string> items,
        IReadOnlySet<string> traits)
    {
        Heroes.Clear();
        foreach (var hero in heroes.OrderBy(static name => name, StringComparer.Ordinal))
        {
            Heroes.Add(hero);
        }

        Items.Clear();
        foreach (var item in items.OrderBy(static name => name, StringComparer.Ordinal))
        {
            Items.Add(item);
        }

        Traits.Clear();
        foreach (var trait in traits.OrderBy(static name => name, StringComparer.Ordinal))
        {
            Traits.Add(trait);
        }
    }

    private void RebuildCandidates(string seasonId)
    {
        PendingCandidates.Clear();
        foreach (var candidate in _dictionaryStore.GetPendingCandidates(seasonId))
        {
            if (!string.IsNullOrWhiteSpace(candidate.Entity))
            {
                PendingCandidates.Add(candidate.Entity);
            }
        }
    }

    private void RebuildComps(string seasonId)
    {
        Comps.Clear();

        var directory = ResolvePatchDirectory(seasonId);
        if (directory is null || !Directory.Exists(directory))
        {
            // 未加载 / 无 meta 数据 → 保持空列表, 由状态栏标注「暂无/待导入」。
            return;
        }

        var store = new MetaInfoStore();
        store.Load(directory);

        var tiersByComp = new Dictionary<string, TierEntry>(StringComparer.Ordinal);
        foreach (var tier in store.QueryTiers())
        {
            if (!string.IsNullOrWhiteSpace(tier.CompId))
            {
                tiersByComp[tier.CompId] = tier;
            }
        }

        foreach (var comp in store.Comps)
        {
            tiersByComp.TryGetValue(comp.Id, out var tierEntry);
            Comps.Add(new CompDisplay(
                comp.Name,
                tierEntry?.Tier,
                comp.CompCode,
                FormatRequirements(comp.IntrinsicRequirements),
                FormatConditionals(tierEntry?.Conditionals)));
        }
    }

    private string? ResolvePatchDirectory(string seasonId)
    {
        if (string.IsNullOrWhiteSpace(_metaRootDirectory))
        {
            return null;
        }

        // 先按 SeasonRegistry 映射 seasonId → patch; 无映射时回退为 seasonId 目录名(兼容 META-SEED 的 data/meta/S18 布局)。
        var directoryName = _registry.MapSeasonToPatch(seasonId) ?? seasonId;
        return Path.Combine(_metaRootDirectory, directoryName);
    }

    private string ComposeStatus()
    {
        var summary = $"奕子 {Heroes.Count} · 装备 {Items.Count} · 羁绊 {Traits.Count} · 候选 {PendingCandidates.Count} · 阵容 {Comps.Count}";
        return Comps.Count == 0 ? summary + " · 暂无阵容数据(待导入)" : summary;
    }

    private static string FormatRequirements(IntrinsicRequirements? requirements)
    {
        if (requirements is null)
        {
            return "无前置";
        }

        var parts = new List<string>();
        if (requirements.RequiredTwoStarUnits is { Count: > 0 })
        {
            parts.Add(string.Join("、", requirements.RequiredTwoStarUnits.Select(static u => $"{u.UnitName}×{u.MinCount}")));
        }

        if (requirements.RequiredEmblems is { Count: > 0 })
        {
            parts.Add("转职:" + string.Join("、", requirements.RequiredEmblems));
        }

        if (requirements.RequiredItems is { Count: > 0 })
        {
            parts.Add("装备:" + string.Join("、", requirements.RequiredItems));
        }

        if (requirements.RequiredAugments is { Count: > 0 })
        {
            parts.Add("强化:" + string.Join("、", requirements.RequiredAugments));
        }

        if (requirements.MinUnitCounts is { Count: > 0 })
        {
            parts.Add(string.Join("、", requirements.MinUnitCounts.Select(static u => $"{u.UnitName}≥{u.MinCount}")));
        }

        return parts.Count == 0 ? "无前置" : string.Join(" · ", parts);
    }

    private static string FormatConditionals(IReadOnlyList<ConditionalTierShift>? conditionals)
    {
        if (conditionals is null or { Count: 0 })
        {
            return "—";
        }

        return string.Join("; ", conditionals.Select(static c =>
        {
            var tierText = c.ShiftedTier.HasValue ? CompDisplay.TierText(c.ShiftedTier.Value) : string.Empty;
            var effect = string.IsNullOrWhiteSpace(c.Effect) ? string.Empty : $"({c.Effect})";
            return $"{c.Condition} → {tierText}{effect}".TrimEnd();
        }));
    }
}