using System.Collections.ObjectModel;
using ChanSight.Core.Annotation;
using ChanSight.Core.Engine;
using ChanSight.Core.Season;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.Services;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChanSight.Overlay.ViewModels;

/// <summary>展示槽位: 单格英雄/星级/费用 + 是否正显示粘滞修正值。</summary>
public partial class SlotDisplay : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(Row))]
    [NotifyPropertyChangedFor(nameof(Col))]
    [NotifyPropertyChangedFor(nameof(HexX))]
    [NotifyPropertyChangedFor(nameof(HexY))]
    private int _index;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(CorrectionMark))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private int _star;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private int _cost;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(CorrectionMark))]
    private bool _hasCorrection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemsText))]
    private string _items = string.Empty;

    public string DisplayText =>
        string.IsNullOrEmpty(Name)
            ? (Index + 1).ToString()
            : Name + (Cost > 0 ? $"({Cost}费)" : Star > 0 ? $"★{Star}" : string.Empty) + CorrectionMark;

    public string CorrectionMark => HasCorrection ? " ✓改" : string.Empty;

    /// <summary>装备列表展示(逗号分隔), 为空返回空串。</summary>
    public string ItemsText => string.IsNullOrWhiteSpace(Items) ? string.Empty : $"🛡 {Items}";

    /// <summary>棋盘 4×7 网格行号(0-3)。仅棋盘格有意义, 其余区域按序号自然计算。</summary>
    public int Row => Index / 7;

    /// <summary>棋盘 4×7 网格列号(0-6)。</summary>
    public int Col => Index % 7;

    /// <summary>棋盘六角布局的像素 X 坐标(交错行右移半个步距)。仅棋盘使用。</summary>
    public double HexX => Col * BoardGridLayout.PitchX + (Row % 2 == 1 ? BoardGridLayout.Stagger : 0);

    /// <summary>棋盘六角布局的像素 Y 坐标。仅棋盘使用。</summary>
    public double HexY => Row * BoardGridLayout.PitchY;

    /// <summary>该格是否为空(未识别到英雄)。用于空格的弱化显示。</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Name);
}

/// <summary>推荐列表展示项。SourceTag 标注来源("算法"/"LLM"), 时间戳便于回溯。</summary>
public sealed record RecommendationDisplay(string SourceTag, string Verdict, string Reason, int Priority, DateTimeOffset GeneratedAt);

/// <summary>LLM 单条建议展示项(来源恒为 "LLM")。</summary>
public sealed record AdvisorDisplay(string SourceTag, string Suggestion, string Reason, double Confidence, DateTimeOffset GeneratedAt);

/// <summary>数据健康度: 自动识别产物为“未校准”, 手动 VLM 识别为“已校准”。</summary>
public enum DataHealth
{
    Uncalibrated,
    Calibrated,
}

/// <summary>手动整帧 VLM 识别闭包: 已取当前帧整图, 返回识别结果。可注入以便测试。</summary>
public delegate Task<RecognitionFrame> ManualRecognizeFunc(bool isSelf, CancellationToken ct);

/// <summary>
/// 实时窗口 ViewModel: 展示 Gold/Level/Stage/Hp + 棋盘(28)/备战席(9)/商店(5)格;
/// 支持钉帧修正(仅记录 CorrectionRecord, 锚 PinnedFrameId)、修正粘滞、手动重算。
/// 不自动重算; 修正不落 GoldLabel(属 UI-REVIEW)。
/// </summary>
public partial class LiveViewModel : ObservableObject
{
    /// <summary>算法推荐自动重跑去抖时长(毫秒)。</summary>
    private const int AlgorithmRefreshDebounceMs = 300;

    private const string SourceTagAlgorithm = "算法";
    private const string SourceTagLlm = "LLM";

    private readonly AnnotationStore _annotationStore;
    private readonly Func<GameStateSnapshot, DecisionPanelResult> _evaluate;
    private readonly Func<GameStateSnapshot, AdvisorEvent, CancellationToken, Task<DecisionPanelResult>>? _evaluateWithAdvisor;
    private readonly StickyCorrections _sticky = new();
    private readonly SeasonRuntime _runtime;
    private readonly ManualRecognizeFunc? _manualRecognize;
    private readonly RecognitionToGameStateAdapter? _adapter;

    private GameStateSnapshot? _latest;
    private RecognitionFrame? _latestFrame;
    private string? _latestFrameId;
    private RecognitionFrame? _pendingOpponentFrame;
    private CancellationTokenSource? _algorithmRefreshCts;

    [ObservableProperty]
    private int _gold;

    [ObservableProperty]
    private int _level;

    [ObservableProperty]
    private string _stage = string.Empty;

    [ObservableProperty]
    private int _hp;

    [ObservableProperty]
    private string _status = "等待识别…";

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private string? _pinnedFrameId;

    [ObservableProperty]
    private int _selectedRegionTypeIndex;

    [ObservableProperty]
    private int _selectedCellIndex = -1;

    [ObservableProperty]
    private string? _selectedHero;

    [ObservableProperty]
    private int _selectedStar;

    /// <summary>对手序号(-1 表示未知/待确认)。绑定到下拉选择。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpponentIndexUnknown))]
    private int _opponentIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HealthLabel))]
    [NotifyPropertyChangedFor(nameof(IsUncalibrated))]
    [NotifyPropertyChangedFor(nameof(IsStale))]
    [NotifyPropertyChangedFor(nameof(StaleWarning))]
    [NotifyPropertyChangedFor(nameof(RecommendationOpacity))]
    private DataHealth _dataHealth = DataHealth.Uncalibrated;

    /// <summary>对手识别后的展示信息(玩家名 + 经济档)。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpponentInfo))]
    private string _opponentInfo = string.Empty;

    /// <summary>待提交的装备列表(逗号分隔, 用于点格装备修正)。</summary>
    [ObservableProperty]
    private string _selectedItems = string.Empty;

    public ObservableCollection<SlotDisplay> BoardCells { get; } = new();

    public ObservableCollection<SlotDisplay> BenchCells { get; } = new();

    public ObservableCollection<SlotDisplay> ShopCards { get; } = new();

    /// <summary>算法推荐展示清单(来源标签为「算法」)。</summary>
    public ObservableCollection<RecommendationDisplay> AlgorithmRecommendations { get; } = new();

    /// <summary>算法推荐清单(兼容旧名, 与 <see cref="AlgorithmRecommendations"/> 同源)。</summary>
    public ObservableCollection<RecommendationDisplay> Recommendations => AlgorithmRecommendations;

    /// <summary>LLM 最近一条建议(仅手动触发, 缓存最近 1 条)。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLlmAdvice))]
    [NotifyPropertyChangedFor(nameof(HasNoLlmAdvice))]
    private AdvisorDisplay? _llmAdvice;

    /// <summary>推荐区是否因数据"未校准"而置灰弱化。</summary>
    public bool IsStale => DataHealth == DataHealth.Uncalibrated;

    /// <summary>推荐区展示透明度(未校准时视觉弱化, 但保留内容)。</summary>
    public double RecommendationOpacity => IsStale ? 0.45 : 1.0;

    /// <summary>健康度未校准时推荐区顶部警告文案。</summary>
    public string StaleWarning => IsStale ? "⚠ 数据未校准, 推荐可能不准" : string.Empty;

    /// <summary>是否已有 LLM 建议可展示。</summary>
    public bool HasLlmAdvice => LlmAdvice is not null;

    /// <summary>是否尚未有 LLM 建议(控制占位提示)。</summary>
    public bool HasNoLlmAdvice => LlmAdvice is null;

    /// <summary>对手确认后的棋盘展示格。</summary>
    public ObservableCollection<SlotDisplay> OpponentBoardCells { get; } = new();

    /// <summary>对手确认后的备战席展示格。</summary>
    public ObservableCollection<SlotDisplay> OpponentBenchCells { get; } = new();

    /// <summary>英雄候选(切换赛季时重建)。用 ObservableCollection 以便 ComboBox 实时刷新。</summary>
    public ObservableCollection<string> HeroCandidates { get; } = new();

    /// <summary>装备候选(切换赛季时重建)。</summary>
    public ObservableCollection<string> ItemCandidates { get; } = new();

    /// <summary>对手序号可选项(0-7), 7 表示“未知”。</summary>
    public IReadOnlyList<string> OpponentIndexOptions { get; } =
    [
        "0",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "未知",
    ];

    /// <summary>可选赛季清单(内置清单, 供 SeasonRegistry 映射后续接入)。</summary>
    public ObservableCollection<string> SeasonOptions { get; } = new();

    /// <summary>可选模式清单。</summary>
    public ObservableCollection<string> ModeOptions { get; } = new();

    [ObservableProperty]
    private string _selectedSeason = SeasonDictionarySeed.DefaultSeasonId;

    [ObservableProperty]
    private string _selectedMode = SeasonDictionarySeed.DefaultMode;

    /// <summary>手动识别是否可用(已注入手动识别闭包)。</summary>
    public bool ManualAvailable => _manualRecognize is not null;

    /// <summary>对手序号是否“未知”(-1 或越界)。</summary>
    public bool IsOpponentIndexUnknown => OpponentIndex < 0 || OpponentIndex > 6;

    /// <summary>健康度文案。</summary>
    public string HealthLabel => DataHealth == DataHealth.Calibrated ? "已校准" : "未校准";

    /// <summary>是否“未校准”(用于显示黄色警告提示)。</summary>
    public bool IsUncalibrated => DataHealth == DataHealth.Uncalibrated;

    /// <summary>是否有对手识别结果可展示。</summary>
    public bool HasOpponentInfo => !string.IsNullOrWhiteSpace(OpponentInfo);

    public LiveViewModel(
        AnnotationStore annotationStore,
        Func<GameStateSnapshot, DecisionPanelResult> evaluate,
        LiveRecognitionService? service = null,
        ManualRecognizeFunc? manualRecognize = null,
        RecognitionToGameStateAdapter? adapter = null,
        Func<GameStateSnapshot, AdvisorEvent, CancellationToken, Task<DecisionPanelResult>>? evaluateWithAdvisor = null,
        SeasonRuntime? runtime = null)
    {
        _annotationStore = annotationStore ?? throw new ArgumentNullException(nameof(annotationStore));
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
        _manualRecognize = manualRecognize;
        _adapter = adapter;
        _evaluateWithAdvisor = evaluateWithAdvisor;
        _runtime = runtime ?? SeasonRuntime.CreateDefault();

        foreach (var id in BuiltInSeasonOptions)
        {
            SeasonOptions.Add(id);
        }

        foreach (var mode in BuiltInModeOptions)
        {
            ModeOptions.Add(mode);
        }

        _selectedSeason = _runtime.Context.SeasonId;
        _selectedMode = _runtime.Context.Mode;
        RebuildCandidates();

        if (service is not null)
        {
            service.FrameUpdated += OnFrameUpdated;
        }
    }

    private static readonly IReadOnlyList<string> BuiltInSeasonOptions =
    [
        "S16.5",
        "S18",
    ];

    private static readonly IReadOnlyList<string> BuiltInModeOptions =
    [
        "恭喜发财",
        "匹配",
        "狂暴",
    ];

    /// <summary>切换赛季 → 调 <see cref="SeasonRuntime.Select"/> 并按新赛季重建候选。</summary>
    partial void OnSelectedSeasonChanged(string value)
    {
        SwitchSeason(value, SelectedMode);
    }

    /// <summary>切换模式 → 调 <see cref="SeasonRuntime.Select"/> 并按新模式生效(字典按 season, prompt/算法按 mode)。</summary>
    partial void OnSelectedModeChanged(string value)
    {
        SwitchSeason(SelectedSeason, value);
    }

    private void SwitchSeason(string seasonId, string mode)
    {
        if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        _runtime.Select(seasonId, mode);
        RebuildCandidates();
        Status = $"已切换赛季 {seasonId} · 模式 {mode}";
    }

    /// <summary>重读 runtime.Heroes/Items 重建候选列表(切换即生效, 无重启)。</summary>
    private void RebuildCandidates()
    {
        HeroCandidates.Clear();
        foreach (var hero in _runtime.Heroes.OrderBy(static name => name, StringComparer.Ordinal))
        {
            HeroCandidates.Add(hero);
        }

        ItemCandidates.Clear();
        foreach (var item in _runtime.Items.OrderBy(static name => name, StringComparer.Ordinal))
        {
            ItemCandidates.Add(item);
        }

        SelectedHero = null;
    }

    /// <summary>把后台链路的一次产出应用到展示字段与粘滞校验。可在任意线程调用; 绑定由 Avalonia 处理。</summary>
    public void ApplyUpdate(LiveFrameUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (IsPinned)
        {
            // 钉帧期间冻结展示。
            return;
        }

        _latest = update.State;
        _latestFrame = update.Frame;
        _latestFrameId = update.FrameId;

        Gold = update.State.Gold;
        Level = update.State.Level;
        Stage = update.State.Stage;
        Hp = update.State.Hp;

        // 自动识别链路产物 = 未校准; 手动 VLM 识别后才会切换为已校准。
        DataHealth = DataHealth.Uncalibrated;

        RebuildCells(update.State);
        ValidateSticky();
        Status = $"帧 {update.FrameId ?? "(未持久化)"} · {update.State.Phase} · 阶段 {update.State.Stage}";

        // 数据更新后算法推荐自动重跑(去抖); LLM 推荐不自动重跑。
        ScheduleAlgorithmRefresh();
    }

    /// <summary>数据更新后按去抖时长调度算法推荐自动重跑。</summary>
    private void ScheduleAlgorithmRefresh()
    {
        _algorithmRefreshCts?.Cancel();
        _algorithmRefreshCts?.Dispose();
        _algorithmRefreshCts = new CancellationTokenSource();
        var token = _algorithmRefreshCts.Token;

        _ = Task.Delay(AlgorithmRefreshDebounceMs, token).ContinueWith(
            _ =>
            {
                if (!token.IsCancellationRequested)
                {
                    RefreshAlgorithmRecommendations();
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    /// <summary>进入钉帧态: 冻结展示, 记录当前用户看到的 frameId。</summary>
    public void PinNow()
    {
        if (_latest is null)
        {
            return;
        }

        IsPinned = true;
        PinnedFrameId = _latestFrameId;
    }

    public void CancelPinning()
    {
        IsPinned = false;
        PinnedFrameId = null;
        SelectedCellIndex = -1;
        SelectedHero = null;
        SelectedStar = 0;
    }

    /// <summary>当前对棋盘/备战席/商店的英雄修正 RegionType 与槽位选择器。</summary>
    public IReadOnlyList<string> CellRegionTypes { get; } =
    [
        RegionTypes.BoardHero,
        RegionTypes.BenchHero,
        RegionTypes.ShopHero,
    ];

    /// <summary>
    /// 提交英雄修正。RegionType 为字段级(board.hero/bench.hero/shop.hero), 锚 PinnedFrameId。
    /// </summary>
    public async Task CommitHeroCorrectionAsync(string regionType, int cellIndex, string heroName)
    {
        if (!IsPinned || PinnedFrameId is null)
        {
            return;
        }

        var recognized = RecognizedFor(regionType, cellIndex);
        var record = CorrectionFactory.Create(
            id: Guid.NewGuid().ToString("N"),
            matchId: ResolveMatchId(),
            frameId: PinnedFrameId,
            snapshotVersion: _latest?.Version ?? 0,
            regionType: regionType,
            cellIndex: cellIndex,
            recognizedValue: recognized,
            correctedValue: CorrectionValue.Hero(heroName));

        await _annotationStore.AppendCorrectionAsync(record).ConfigureAwait(false);
        _sticky.Set(new StickyEntry(regionType, cellIndex, recognized, CorrectionValue.Hero(heroName)));
        RebuildStickyMarkers();
    }

    /// <summary>提交星级修正。</summary>
    public async Task CommitStarCorrectionAsync(string regionType, int cellIndex, int star)
    {
        if (!IsPinned || PinnedFrameId is null)
        {
            return;
        }

        var recognized = RecognizedFor(regionType, cellIndex);
        var record = CorrectionFactory.Create(
            id: Guid.NewGuid().ToString("N"),
            matchId: ResolveMatchId(),
            frameId: PinnedFrameId,
            snapshotVersion: _latest?.Version ?? 0,
            regionType: regionType,
            cellIndex: cellIndex,
            recognizedValue: recognized,
            correctedValue: CorrectionValue.Star(star));

        await _annotationStore.AppendCorrectionAsync(record).ConfigureAwait(false);
        _sticky.Set(new StickyEntry(regionType, cellIndex, recognized, CorrectionValue.Star(star)));
        RebuildStickyMarkers();
    }

    /// <summary>提交“空格”修正(Mark empty)。</summary>
    public async Task CommitEmptyCorrectionAsync(string regionType, int cellIndex)
    {
        if (!IsPinned || PinnedFrameId is null)
        {
            return;
        }

        var recognized = RecognizedFor(regionType, cellIndex);
        var record = CorrectionFactory.Create(
            id: Guid.NewGuid().ToString("N"),
            matchId: ResolveMatchId(),
            frameId: PinnedFrameId,
            snapshotVersion: _latest?.Version ?? 0,
            regionType: regionType,
            cellIndex: cellIndex,
            recognizedValue: recognized,
            correctedValue: CorrectionValue.Empty());

        await _annotationStore.AppendCorrectionAsync(record).ConfigureAwait(false);
        _sticky.Set(new StickyEntry(regionType, cellIndex, recognized, CorrectionValue.Empty()));
        RebuildStickyMarkers();
    }

    /// <summary>提交装备列表修正。</summary>
    public async Task CommitItemsCorrectionAsync(string regionType, int cellIndex, IReadOnlyList<string> items)
    {
        if (!IsPinned || PinnedFrameId is null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(items);

        var recognized = RecognizedFor(regionType, cellIndex);
        var record = CorrectionFactory.Create(
            id: Guid.NewGuid().ToString("N"),
            matchId: ResolveMatchId(),
            frameId: PinnedFrameId,
            snapshotVersion: _latest?.Version ?? 0,
            regionType: regionType,
            cellIndex: cellIndex,
            recognizedValue: recognized,
            correctedValue: CorrectionValue.Items(items));

        await _annotationStore.AppendCorrectionAsync(record).ConfigureAwait(false);
        _sticky.Set(new StickyEntry(regionType, cellIndex, recognized, CorrectionValue.Items(items)));
        RebuildStickyMarkers();
    }

    /// <summary>手动重算: 用粘滞修正后的状态刷新算法推荐列表。仅按钮触发, 不自动调用。</summary>
    public void RecalculateRecommendations() => RefreshAlgorithmRecommendations();

    /// <summary>用当前(粘滞修正后)状态刷新算法推荐列表, 来源标签标「算法」。自动/手动共用。</summary>
    public void RefreshAlgorithmRecommendations()
    {
        var state = BuildCorrectedSnapshot();
        var result = _evaluate(state);
        var generatedAt = DateTimeOffset.Now;

        AlgorithmRecommendations.Clear();
        foreach (var advice in result.AlgorithmAdvice.OrderByDescending(static r => r.Priority))
        {
            AlgorithmRecommendations.Add(new RecommendationDisplay(
                SourceTagAlgorithm,
                advice.Verdict.ToString(),
                advice.Reason,
                advice.Priority,
                generatedAt));
        }

        Status = $"已重算 · 算法推荐 {AlgorithmRecommendations.Count} 条";
    }

    /// <summary>手动触发 LLM 全局建议(ManualRequest), 缓存最近 1 条, 来源标签标「LLM」。</summary>
    public async Task RequestLlmAdviceAsync()
    {
        if (_evaluateWithAdvisor is null)
        {
            Status = "LLM 建议不可用(未注入建议闭包)。";
            return;
        }

        var state = BuildCorrectedSnapshot();
        var context = BuildSnapshotSummary(state);
        Status = "正在请求 LLM 建议…";

        try
        {
            var result = await _evaluateWithAdvisor(
                state,
                new AdvisorEvent(AdvisorEventType.ManualRequest, context),
                CancellationToken.None).ConfigureAwait(true);

            if (result.AdvisorAdvice is null)
            {
                Status = "LLM 未返回建议。";
                return;
            }

            LlmAdvice = new AdvisorDisplay(
                SourceTagLlm,
                result.AdvisorAdvice.Suggestion,
                result.AdvisorAdvice.Reason,
                result.AdvisorAdvice.Confidence,
                DateTimeOffset.Now);
            Status = "已更新 LLM 建议(缓存最近 1 条)。";
        }
        catch (Exception ex)
        {
            Status = $"请求 LLM 建议失败: {ex.Message}";
        }
    }

    /// <summary>构造手动 LLM 建议的事件上下文快照摘要(阶段/金币/等级/己方阵容/对手摘要)。</summary>
    private static string BuildSnapshotSummary(GameStateSnapshot state)
    {
        var selfUnits = string.Join("、", state.BoardUnits
            .Where(u => !string.IsNullOrEmpty(u.Name))
            .Select(u => u.Name + (u.Star > 0 ? $"★{u.Star}" : string.Empty)));
        if (string.IsNullOrEmpty(selfUnits))
        {
            selfUnits = "(空棋盘)";
        }

        var opponents = state.Opponents.Count == 0
            ? "无已确认对手"
            : string.Join("; ", state.Opponents.Select(o =>
                $"{o.PlayerName ?? $"对手{o.PlayerIndex}"} 金币~{o.GoldEstimate} 等级{o.Level?.ToString() ?? "?"}"));

        return $"阶段 {state.Stage} · 金币 {state.Gold} · 等级 {state.Level} · HP {state.Hp} · " +
               $"己方阵容: {selfUnits} · 对手: {opponents}";
    }

    /// <summary>当前最新帧 Id(用于钉帧选择被钉帧; 钉帧态下仍返回被钉帧)。</summary>
    public string? CurrentFrameId => IsPinned ? PinnedFrameId : _latestFrameId;

    [RelayCommand]
    private void EnterPin() => PinNow();

    [RelayCommand]
    private void CancelPin() => CancelPinning();

    [RelayCommand]
    private async Task CommitHero()
    {
        var regionType = CellRegionTypes[SelectedRegionTypeIndex];
        if (SelectedHero is not null && SelectedCellIndex >= 0)
        {
            await CommitHeroCorrectionAsync(regionType, SelectedCellIndex, SelectedHero).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CommitStar()
    {
        if (SelectedCellIndex >= 0)
        {
            await CommitStarCorrectionAsync(RegionTypes.BoardStar, SelectedCellIndex, SelectedStar).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CommitEmpty()
    {
        var regionType = CellRegionTypes[SelectedRegionTypeIndex];
        if (SelectedCellIndex >= 0)
        {
            await CommitEmptyCorrectionAsync(regionType, SelectedCellIndex).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CommitItems()
    {
        if (SelectedCellIndex < 0)
        {
            return;
        }

        var items = ParseSelectedItems(SelectedItems);
        await CommitItemsCorrectionAsync(RegionTypes.BoardItems, SelectedCellIndex, items).ConfigureAwait(true);
    }

    [RelayCommand]
    private void Recalculate() => RecalculateRecommendations();

    [RelayCommand]
    private void RefreshAlgorithm() => RefreshAlgorithmRecommendations();

    [RelayCommand]
    private Task RequestLlmAdvice() => RequestLlmAdviceAsync();

    private void OnFrameUpdated(LiveFrameUpdate update) => ApplyUpdate(update);

    /// <summary>
    /// 手动触发整帧 VLM 识别。isSelf=true 识别己方并写 GameStateManager; isSelf=false
    /// 识别对手, 结果暂存并等待用户确认/修正对手序号后才写槽。
    /// 不自动重算推荐(由后续 DUAL-RECO 决定)。
    /// </summary>
    [RelayCommand]
    private Task ManualRecognizeAsync(bool isSelf) => RunManualRecognitionAsync(isSelf);

    [RelayCommand]
    private Task ManualRecognizeSelf() => RunManualRecognitionAsync(true);

    [RelayCommand]
    private Task ManualRecognizeOpponent() => RunManualRecognitionAsync(false);

    public async Task RunManualRecognitionAsync(bool isSelf)
    {
        if (_manualRecognize is null)
        {
            Status = "手动识别不可用(未注入识别闭包)。";
            return;
        }

        Status = "手动识别中…";
        try
        {
            var frame = await _manualRecognize(isSelf, CancellationToken.None).ConfigureAwait(true);

            if (isSelf)
            {
                _adapter?.Apply(frame);
                Gold = frame.Gold;
                Level = frame.Level;
                Stage = string.IsNullOrWhiteSpace(frame.Stage) ? Stage : frame.Stage;
                Hp = frame.Hp;
                DataHealth = DataHealth.Calibrated;
                Status = ComposeRecognitionStatus(frame, self: true);
            }
            else
            {
                _pendingOpponentFrame = frame;
                OpponentIndex = frame.OpponentIndex is >= 0 and <= 6 ? frame.OpponentIndex.Value : -1;
                OpponentInfo = BuildOpponentInfo(frame);
                DataHealth = DataHealth.Calibrated;
                Status = IsOpponentIndexUnknown
                    ? "对手识别完成, 请确认对手序号…"
                    : ComposeRecognitionStatus(frame, self: false);
            }
        }
        catch (Exception ex)
        {
            Status = $"手动识别失败: {ex.Message}";
        }
    }

    /// <summary>用户确认(或修正)对手序号后, 把待确认的对手识别结果写入对应 OpponentSnapshot。</summary>
    [RelayCommand]
    private void ConfirmOpponent()
    {
        if (_pendingOpponentFrame is null)
        {
            Status = "无待确认的对手识别结果。";
            return;
        }

        if (IsOpponentIndexUnknown || _adapter is null)
        {
            Status = "请先选择对手序号(0-6)。";
            return;
        }

        _adapter.ApplyOpponent(_pendingOpponentFrame, OpponentIndex);
        RebuildOpponentCells(_pendingOpponentFrame);
        Status = $"已写入对手 #{OpponentIndex} ({_pendingOpponentFrame.PlayerName ?? "未知玩家"})";
    }

    private void RebuildOpponentCells(RecognitionFrame frame)
    {
        OpponentBoardCells.Clear();
        foreach (var (cell, index) in frame.BoardCells.Select((c, i) => (c, i)))
        {
            OpponentBoardCells.Add(new SlotDisplay
            {
                Index = index,
                Name = cell.Name ?? string.Empty,
                Star = cell.Star,
                Items = cell.Items is null ? string.Empty : string.Join(",", cell.Items.Select(i => i.IconId)),
            });
        }

        OpponentBenchCells.Clear();
        foreach (var (cell, index) in frame.BenchCells.Select((c, i) => (c, i)))
        {
            OpponentBenchCells.Add(new SlotDisplay
            {
                Index = index,
                Name = cell.Name ?? string.Empty,
                Star = cell.Star,
                Items = cell.Items is null ? string.Empty : string.Join(",", cell.Items.Select(i => i.IconId)),
            });
        }
    }

    private static string ComposeRecognitionStatus(RecognitionFrame frame, bool self)
    {
        var issueCount = frame.Issues?.Count ?? 0;
        var baseText = self
            ? $"手动识别完成 · 己方 · {issueCount} 个 issue"
            : $"手动识别完成 · 对手 · {issueCount} 个 issue";

        return issueCount > 0 ? baseText + " · 识别不完整,请人工核对" : baseText;
    }

    private static string BuildOpponentInfo(RecognitionFrame frame)
    {
        var gold = frame.GoldEstimate.HasValue ? $"{frame.GoldEstimate.Value}+" : "未知";
        return $"{frame.PlayerName ?? "未知玩家"} · 经济 {gold}";
    }

    private string ResolveMatchId() => LiveRecognitionService.DefaultMatchId;

    private void RebuildCells(GameStateSnapshot state)
    {
        BoardCells.Clear();
        foreach (var unit in state.BoardUnits)
        {
            BoardCells.Add(new SlotDisplay
            {
                Index = unit.SlotIndex,
                Name = unit.Name ?? string.Empty,
                Star = unit.Star,
                Items = unit.Items is null ? string.Empty : string.Join(",", unit.Items),
            });
        }

        BenchCells.Clear();
        foreach (var unit in state.BenchUnits)
        {
            BenchCells.Add(new SlotDisplay
            {
                Index = unit.SlotIndex,
                Name = unit.Name ?? string.Empty,
                Star = unit.Star,
                Items = unit.Items is null ? string.Empty : string.Join(",", unit.Items),
            });
        }

        ShopCards.Clear();
        foreach (var card in state.ShopCards)
        {
            ShopCards.Add(new SlotDisplay
            {
                Index = card.SlotIndex,
                Name = card.Name ?? string.Empty,
                Cost = card.Cost,
            });
        }
    }

    private string? RecognizedFor(string regionType, int cellIndex)
    {
        if (_latest is null)
        {
            return null;
        }

        return regionType switch
        {
            RegionTypes.BoardHero => cellIndex < _latest.BoardUnits.Count ? _latest.BoardUnits[cellIndex].Name : null,
            RegionTypes.BoardStar => cellIndex < _latest.BoardUnits.Count ? _latest.BoardUnits[cellIndex].Star.ToString() : null,
            RegionTypes.BoardItems => cellIndex < _latest.BoardUnits.Count ? JoinItems(_latest.BoardUnits[cellIndex].Items) : null,
            RegionTypes.BenchHero => cellIndex < _latest.BenchUnits.Count ? _latest.BenchUnits[cellIndex].Name : null,
            RegionTypes.ShopHero => cellIndex < _latest.ShopCards.Count ? _latest.ShopCards[cellIndex].Name : null,
            _ => null,
        };
    }

    private static string? JoinItems(IReadOnlyList<string>? items) =>
        items is null || items.Count == 0 ? null : string.Join(",", items);

    private static IReadOnlyList<string> ParseSelectedItems(string raw) =>
        (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

    private void ValidateSticky()
    {
        var keys = _sticky.Entries.Keys.ToList();
        foreach (var key in keys)
        {
            var entry = _sticky.Entries[key];
            _ = _sticky.IsSticky(entry.RegionType, entry.CellIndex, RecognizedFor(entry.RegionType, entry.CellIndex));
        }

        RebuildStickyMarkers();
    }

    private void RebuildStickyMarkers() => ApplyStickyMarkers();

    private void ApplyStickyMarkers()
    {
        foreach (var cell in BoardCells)
        {
            var hero = _sticky.Get(RegionTypes.BoardHero, cell.Index);
            var star = _sticky.Get(RegionTypes.BoardStar, cell.Index);
            var items = _sticky.Get(RegionTypes.BoardItems, cell.Index);
            cell.HasCorrection = hero is not null || star is not null || items is not null;

            if (hero is not null)
            {
                var correctedHero = ParseHero(hero.Corrected);
                cell.Name = correctedHero ?? string.Empty;
                if (correctedHero is null)
                {
                    cell.Star = 0;
                }
            }

            if (star is not null)
            {
                cell.Star = ParseStar(star.Corrected);
            }

            if (items is not null)
            {
                cell.Items = JoinCorrectedItems(ParseItems(items.Corrected));
            }
        }

        foreach (var cell in BenchCells)
        {
            var hero = _sticky.Get(RegionTypes.BenchHero, cell.Index);
            cell.HasCorrection = hero is not null;

            if (hero is not null)
            {
                var correctedHero = ParseHero(hero.Corrected);
                cell.Name = correctedHero ?? string.Empty;
            }
        }

        foreach (var cell in ShopCards)
        {
            var hero = _sticky.Get(RegionTypes.ShopHero, cell.Index);
            cell.HasCorrection = hero is not null;

            if (hero is not null)
            {
                var correctedHero = ParseHero(hero.Corrected);
                cell.Name = correctedHero ?? string.Empty;
                if (correctedHero is null)
                {
                    cell.Cost = 0;
                }
            }
        }
    }

    /// <summary>把仍生效的粘滞修正叠加到当前快照, 产出用于重算的“修正后状态”。</summary>
    public GameStateSnapshot BuildCorrectedSnapshot()
    {
        var state = _latest ?? CreateEmptySnapshot();

        var board = state.BoardUnits.ToList();
        var bench = state.BenchUnits.ToList();
        var shop = state.ShopCards.ToList();

        foreach (var entry in _sticky.Entries.Values)
        {
            if (entry.RegionType == RegionTypes.BoardHero || entry.RegionType == RegionTypes.BenchHero)
            {
                var list = entry.RegionType == RegionTypes.BoardHero ? board : bench;
                if (entry.CellIndex < list.Count)
                {
                    list[entry.CellIndex] = ApplyHeroToUnit(list[entry.CellIndex], entry.Corrected);
                }
            }
            else if (entry.RegionType == RegionTypes.BoardStar)
            {
                if (entry.CellIndex < board.Count)
                {
                    board[entry.CellIndex] = ApplyStarToUnit(board[entry.CellIndex], entry.Corrected);
                }
            }
            else if (entry.RegionType == RegionTypes.BoardItems)
            {
                if (entry.CellIndex < board.Count)
                {
                    board[entry.CellIndex] = ApplyItemsToUnit(board[entry.CellIndex], entry.Corrected);
                }
            }
            else if (entry.RegionType == RegionTypes.ShopHero)
            {
                if (entry.CellIndex < shop.Count)
                {
                    shop[entry.CellIndex] = ApplyHeroToShop(shop[entry.CellIndex], entry.Corrected);
                }
            }
        }

        return state with
        {
            BoardUnits = board,
            BenchUnits = bench,
            ShopCards = shop,
        };
    }

    private static BoardUnitState ApplyHeroToUnit(BoardUnitState unit, string corrected)
    {
        var hero = ParseHero(corrected);
        return unit with { Name = hero, Star = hero is null ? 0 : unit.Star };
    }

    private static BoardUnitState ApplyStarToUnit(BoardUnitState unit, string corrected)
    {
        var star = ParseStar(corrected);
        return unit with { Star = star };
    }

    private static BoardUnitState ApplyItemsToUnit(BoardUnitState unit, string corrected)
    {
        var items = ParseItems(corrected);
        return unit with { Items = items };
    }

    private static ShopCardState ApplyHeroToShop(ShopCardState card, string corrected)
    {
        var hero = ParseHero(corrected);
        return card with { Name = hero, Cost = hero is null ? 0 : card.Cost };
    }

    private static string? ParseHero(string corrected)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(corrected);
            var root = doc.RootElement;
            if (root.TryGetProperty("empty", out var empty) && empty.GetBoolean())
            {
                return null;
            }

            if (root.TryGetProperty("hero", out var hero))
            {
                return hero.GetString();
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return null;
    }

    private static int ParseStar(string corrected)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(corrected);
            if (doc.RootElement.TryGetProperty("star", out var star) && star.TryGetInt32(out var value))
            {
                return value;
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return 0;
    }

    private static IReadOnlyList<string> ParseItems(string corrected)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(corrected);
            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return items.EnumerateArray()
                    .Where(i => i.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(i => i.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .ToArray();
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return Array.Empty<string>();
    }

    private static string JoinCorrectedItems(IReadOnlyList<string> items) =>
        items.Count == 0 ? string.Empty : string.Join(",", items);

    private static GameStateSnapshot CreateEmptySnapshot() => new(
        Stage: "1-1",
        Phase: GamePhase.Planning,
        Gold: 0,
        Level: 1,
        Exp: 0,
        Hp: 100,
        Streak: 0,
        PlayerName: null,
        BoardUnits: Array.Empty<BoardUnitState>(),
        BenchUnits: Array.Empty<BoardUnitState>(),
        ShopCards: Array.Empty<ShopCardState>(),
        Opponents: Array.Empty<OpponentSnapshot>(),
        Version: 0);
}