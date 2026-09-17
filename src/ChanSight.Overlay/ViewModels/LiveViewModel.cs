using System.Collections.ObjectModel;
using ChanSight.Core.Annotation;
using ChanSight.Core.Engine;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.Services;
using ChanSight.Vision.Models;
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

    public string DisplayText =>
        string.IsNullOrEmpty(Name)
            ? (Index + 1).ToString()
            : Name + (Cost > 0 ? $"({Cost}费)" : Star > 0 ? $"★{Star}" : string.Empty) + CorrectionMark;

    public string CorrectionMark => HasCorrection ? " ✓改" : string.Empty;

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

/// <summary>推荐列表展示项。</summary>
public sealed record RecommendationDisplay(string Verdict, string Reason, int Priority);

/// <summary>
/// 实时窗口 ViewModel: 展示 Gold/Level/Stage/Hp + 棋盘(28)/备战席(9)/商店(5)格;
/// 支持钉帧修正(仅记录 CorrectionRecord, 锚 PinnedFrameId)、修正粘滞、手动重算。
/// 不自动重算; 修正不落 GoldLabel(属 UI-REVIEW)。
/// </summary>
public partial class LiveViewModel : ObservableObject
{
    private readonly AnnotationStore _annotationStore;
    private readonly Func<GameStateSnapshot, DecisionPanelResult> _evaluate;
    private readonly StickyCorrections _sticky = new();
    private readonly IReadOnlyList<string> _heroCandidates;

    private GameStateSnapshot? _latest;
    private RecognitionFrame? _latestFrame;
    private string? _latestFrameId;

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

    public ObservableCollection<SlotDisplay> BoardCells { get; } = new();

    public ObservableCollection<SlotDisplay> BenchCells { get; } = new();

    public ObservableCollection<SlotDisplay> ShopCards { get; } = new();

    public ObservableCollection<RecommendationDisplay> Recommendations { get; } = new();

    public IReadOnlyList<string> HeroCandidates => _heroCandidates;

    public LiveViewModel(
        AnnotationStore annotationStore,
        Func<GameStateSnapshot, DecisionPanelResult> evaluate,
        LiveRecognitionService? service = null)
    {
        _annotationStore = annotationStore ?? throw new ArgumentNullException(nameof(annotationStore));
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
        _heroCandidates = GameSeasonDictionary.Heroes.OrderBy(static name => name, StringComparer.Ordinal).ToList();

        if (service is not null)
        {
            service.FrameUpdated += OnFrameUpdated;
        }
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

        RebuildCells(update.State);
        ValidateSticky();
        Status = $"帧 {update.FrameId ?? "(未持久化)"} · {update.State.Phase} · 阶段 {update.State.Stage}";
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

    /// <summary>手动重算: 用粘滞修正后的状态刷新推荐列表。仅按钮触发, 不自动调用。</summary>
    public void RecalculateRecommendations()
    {
        var state = BuildCorrectedSnapshot();
        var result = _evaluate(state);

        Recommendations.Clear();
        foreach (var advice in result.AlgorithmAdvice.OrderByDescending(static r => r.Priority))
        {
            Recommendations.Add(new RecommendationDisplay(advice.Verdict.ToString(), advice.Reason, advice.Priority));
        }

        Status = $"已重算 · 推荐 {Recommendations.Count} 条";
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
    private void Recalculate() => RecalculateRecommendations();

    private void OnFrameUpdated(LiveFrameUpdate update) => ApplyUpdate(update);

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
            RegionTypes.BenchHero => cellIndex < _latest.BenchUnits.Count ? _latest.BenchUnits[cellIndex].Name : null,
            RegionTypes.ShopHero => cellIndex < _latest.ShopCards.Count ? _latest.ShopCards[cellIndex].Name : null,
            _ => null,
        };
    }

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
            cell.HasCorrection = hero is not null || star is not null;

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

    private static GameStateSnapshot CreateEmptySnapshot() => new(
        Stage: "1-1",
        Phase: GamePhase.Planning,
        Gold: 0,
        Level: 1,
        Exp: 0,
        Hp: 100,
        Streak: 0,
        BoardUnits: Array.Empty<BoardUnitState>(),
        BenchUnits: Array.Empty<BoardUnitState>(),
        ShopCards: Array.Empty<ShopCardState>(),
        Opponents: Array.Empty<OpponentSnapshot>(),
        Version: 0);
}