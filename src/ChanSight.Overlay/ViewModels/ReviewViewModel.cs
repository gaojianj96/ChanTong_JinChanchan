using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using ChanSight.Core.Annotation;
using ChanSight.Core.FrameStorage;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.Services;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace ChanSight.Overlay.ViewModels;

/// <summary>回顾格展示: 识别到的英雄/星级/来源层级 + 是否被修正及修正值。</summary>
public partial class ReviewCellDisplay : ObservableObject
{
    public string RegionType { get; }
    public int CellIndex { get; }
    public string KindLabel { get; }
    public string RecognizedName { get; }
    public int Star { get; }
    public string SourceTier { get; }
    public IReadOnlyList<string> Items { get; }
    public double Confidence { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(ShortLabel))]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private bool _isCorrected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(ShortLabel))]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private string _correctedHero = string.Empty;

    public string? CorrectionId { get; set; }
    public string? CorrectionType { get; set; }

    public ReviewCellDisplay(
        string regionType,
        int cellIndex,
        string kindLabel,
        string recognizedName,
        int star,
        string sourceTier,
        IReadOnlyList<string>? items = null,
        double confidence = 0.0)
    {
        RegionType = regionType;
        CellIndex = cellIndex;
        KindLabel = kindLabel;
        RecognizedName = recognizedName;
        Star = star;
        SourceTier = sourceTier;
        Items = items ?? Array.Empty<string>();
        Confidence = confidence;
    }

    public string EffectiveName => IsCorrected
        ? (string.IsNullOrEmpty(CorrectedHero) ? "空" : CorrectedHero)
        : RecognizedName;

    /// <summary>棋盘格简写: 英雄名 + 星级(空位显示「空」)。</summary>
    public string ShortLabel
    {
        get
        {
            string name = EffectiveName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return "空";
            }

            return Star > 0 ? $"{name}★{Star}" : name;
        }
    }

    /// <summary>悬停 tooltip 具体文字: 英雄全名 / 星级 / 装备 / 来源 tier / 置信度。</summary>
    public string ToolTipText
    {
        get
        {
            string name = string.IsNullOrWhiteSpace(EffectiveName) ? "空" : EffectiveName;
            string items = Items.Count > 0 ? string.Join("、", Items) : "无";
            string correction = IsCorrected ? $"\n修正类型: {CorrectionTypeLabel}" : string.Empty;

            return $"英雄: {name}\n"
                 + $"星级: {Math.Max(0, Star)}\n"
                 + $"装备: {items}\n"
                 + $"来源 tier: {SourceTier}\n"
                 + $"置信度: {Confidence:P0}{correction}";
        }
    }

    private string CorrectionTypeLabel => CorrectionType switch
    {
        CorrectionTypes.Missed => "漏检",
        CorrectionTypes.FalsePositive => "误检",
        CorrectionTypes.Localization => "定位错",
        CorrectionTypes.Classification => "分类错",
        CorrectionTypes.ConfirmCorrect => "确认正确",
        _ => CorrectionType ?? string.Empty,
    };

    public string DisplayText => IsCorrected
        ? $"[{CellIndex}] {RecognizedName} → {(string.IsNullOrEmpty(CorrectedHero) ? "空" : CorrectedHero)}"
        : $"[{CellIndex}] {RecognizedName}";
}

/// <summary>原图叠框的单个矩形(已换算到渲染空间)。</summary>
public sealed record ReviewBoxDisplay(
    string RegionType,
    int CellIndex,
    string Label,
    double X,
    double Y,
    double Width,
    double Height);

/// <summary>
/// 回顾窗口 ViewModel: 选择 matchId → 列出关键帧 → 选择帧 → 展示原图(叠框)+ 每格识别值;
/// 逐格修正(关联实时 CorrectionRecord), 「确认本帧」时未修正格由服务自动补 confirm-correct。
/// </summary>
public partial class ReviewViewModel : ObservableObject
{
    private const double MaxRenderWidth = 440.0;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 1.25;

    private readonly ReviewService _service;
    private readonly AnnotationStore _store;
    private readonly IRoiMapperService _roiMapper;
    private readonly IReadOnlyList<string> _heroCandidates;

    private ReviewFrame? _currentFrame;

    [ObservableProperty]
    private string _matchId = LiveRecognitionService.DefaultMatchId;

    [ObservableProperty]
    private string _status = "输入对局 ID 后点击「加载关键帧」";

    [ObservableProperty]
    private IReadOnlyList<FrameKey> _keyFrames = Array.Empty<FrameKey>();

    [ObservableProperty]
    private FrameKey? _selectedFrame;

    [ObservableProperty]
    private Bitmap? _frameBitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayWidth))]
    private double _renderWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayHeight))]
    private double _renderHeight;

    /// <summary>原图按缩放因子的实际显示尺寸(供 ScrollViewer 计算滚动范围)。</summary>
    public double DisplayWidth => RenderWidth * Zoom;

    /// <summary>原图按缩放因子的实际显示高度。</summary>
    public double DisplayHeight => RenderHeight * Zoom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayWidth))]
    [NotifyPropertyChangedFor(nameof(DisplayHeight))]
    private double _zoom = 1.0;

    [ObservableProperty]
    private ReviewCellDisplay? _selectedCell;

    [ObservableProperty]
    private string? _selectedHero;

    [ObservableProperty]
    private int _selectedCorrectionTypeIndex;

    public ObservableCollection<ReviewCellDisplay> Cells { get; } = new();

    /// <summary>4×7 虚拟棋盘格(28 个棋盘格, 与 <see cref="Cells"/> 共享同一批对象)。</summary>
    public ObservableCollection<ReviewCellDisplay> BoardCells { get; } = new();

    /// <summary>备战席(9)+ 商店(5)格, 保留逐格修正能力。</summary>
    public ObservableCollection<ReviewCellDisplay> SideCells { get; } = new();

    /// <summary>可用对局 ID 列表(下拉选项; 允许手动输入未列出的 ID)。</summary>
    public ObservableCollection<string> MatchIdOptions { get; } = new();

    [ObservableProperty]
    private IReadOnlyList<ReviewBoxDisplay> _boxes = Array.Empty<ReviewBoxDisplay>();

    public IReadOnlyList<string> HeroCandidates => _heroCandidates;

    /// <summary>修正类型中文标签(与 <see cref="CorrectionTypeCodes"/> 下标一一对应)。</summary>
    public IReadOnlyList<string> CorrectionTypeOptions { get; } =
    [
        "分类错",
        "定位错",
        "漏检",
        "误检",
    ];

    private static readonly IReadOnlyList<string> CorrectionTypeCodes =
    [
        CorrectionTypes.Classification,
        CorrectionTypes.Localization,
        CorrectionTypes.Missed,
        CorrectionTypes.FalsePositive,
    ];

    public ReviewViewModel(ReviewService service, AnnotationStore store, IRoiMapperService? roiMapper = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _roiMapper = roiMapper ?? new RoiMapperService();
        _heroCandidates = GameSeasonDictionary.Heroes.OrderBy(static name => name, StringComparer.Ordinal).ToList();
        RefreshMatches();
    }

    /// <summary>重新枚举归档下已存在的对局 ID, 刷新下拉选项。</summary>
    [RelayCommand]
    private void RefreshMatches()
    {
        MatchIdOptions.Clear();
        foreach (var id in _service.ListMatchIds())
        {
            MatchIdOptions.Add(id);
        }
    }

    /// <summary>按倍率缩放原图(内部钳制到 0.5x~4x)。</summary>
    public void ZoomBy(double factor)
    {
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
    }

    [RelayCommand]
    private void ZoomIn() => ZoomBy(ZoomStep);

    [RelayCommand]
    private void ZoomOut() => ZoomBy(1.0 / ZoomStep);

    [RelayCommand]
    private void ResetZoom() => Zoom = 1.0;

    [RelayCommand]
    private void LoadFrames()
    {
        if (string.IsNullOrWhiteSpace(MatchId))
        {
            Status = "对局 ID 不能为空";
            return;
        }

        KeyFrames = _service.ListKeyFrames(MatchId);
        SelectedFrame = null;
        Cells.Clear();
        BoardCells.Clear();
        SideCells.Clear();
        Boxes = Array.Empty<ReviewBoxDisplay>();
        FrameBitmap = null;
        Status = $"对局 {MatchId}: {KeyFrames.Count} 个关键帧";
    }

    partial void OnSelectedFrameChanged(FrameKey? value)
    {
        if (value is null)
        {
            return;
        }

        LoadSelectedFrame(value);
    }

    /// <summary>确认本帧: 只提交用户修正过的格; 未修正格由 <see cref="ReviewService"/> 自动补 confirm-correct。</summary>
    [RelayCommand]
    private async Task ConfirmFrameAsync()
    {
        if (SelectedFrame is null)
        {
            Status = "请先选择关键帧";
            return;
        }

        var decisions = BuildDecisions();
        var labels = await _service.CommitReviewAsync(MatchId, SelectedFrame.FrameId, decisions).ConfigureAwait(true);

        Status = $"已确认帧 {SelectedFrame.FrameId}: {labels.Count} 条金标(修正 {decisions.Count} 格, 其余自动 confirm-correct)";
    }

    /// <summary>把当前选中格的英雄修正落为实时 CorrectionRecord 并标记为已修正。</summary>
    [RelayCommand]
    private async Task ApplyCorrectionAsync()
    {
        if (SelectedCell is null || string.IsNullOrWhiteSpace(SelectedHero))
        {
            return;
        }

        await ApplyCellCorrectionAsync(SelectedCell, CorrectionValue.Hero(SelectedHero), SelectedHero).ConfigureAwait(true);
    }

    /// <summary>把当前选中格标记为空(false-positive)。</summary>
    [RelayCommand]
    private async Task MarkEmptyAsync()
    {
        if (SelectedCell is null)
        {
            return;
        }

        await ApplyCellCorrectionAsync(SelectedCell, CorrectionValue.Empty(), string.Empty).ConfigureAwait(true);
    }

    public IReadOnlyList<ReviewDecision> BuildDecisions()
    {
        var decisions = new List<ReviewDecision>();
        foreach (var cell in Cells)
        {
            if (!cell.IsCorrected)
            {
                continue;
            }

            string correctedValue = string.IsNullOrEmpty(cell.CorrectedHero)
                ? CorrectionValue.Empty()
                : CorrectionValue.Hero(cell.CorrectedHero);

            decisions.Add(new ReviewDecision(
                cell.RegionType,
                cell.CellIndex,
                null,
                correctedValue,
                cell.CorrectionType ?? CorrectionTypes.Classification,
                cell.CorrectionId));
        }

        return decisions;
    }

    private async Task ApplyCellCorrectionAsync(ReviewCellDisplay cell, string correctedValue, string correctedHero)
    {
        string type = CorrectionTypeCodes[ClampCorrectionTypeIndex()];
        string? recognized = string.IsNullOrWhiteSpace(cell.RecognizedName) ? null : cell.RecognizedName;

        var record = CorrectionFactory.Create(
            id: Guid.NewGuid().ToString("N"),
            matchId: MatchId,
            frameId: SelectedFrame!.FrameId,
            snapshotVersion: SnapshotVersionOf(_currentFrame),
            regionType: cell.RegionType,
            cellIndex: cell.CellIndex,
            recognizedValue: recognized,
            correctedValue: correctedValue);

        await _store.AppendCorrectionAsync(record).ConfigureAwait(true);

        cell.CorrectionId = record.Id;
        cell.CorrectionType = type;
        cell.CorrectedHero = correctedHero;
        cell.IsCorrected = true;
    }

    private void LoadSelectedFrame(FrameKey key)
    {
        _currentFrame?.FrameImage.Dispose();
        _currentFrame = _service.LoadFrame(MatchId, key.FrameId);

        int imageWidth = _currentFrame.FrameImage.Cols;
        int imageHeight = _currentFrame.FrameImage.Rows;

        FrameBitmap = MatToBitmap(_currentFrame.FrameImage);
        _currentFrame.FrameImage.Dispose();

        RenderWidth = Math.Min(MaxRenderWidth, imageWidth);
        RenderHeight = imageWidth > 0 ? RenderWidth * imageHeight / imageWidth : 0;

        RebuildCells(_currentFrame.Recognition);
        RebuildBoxes(imageWidth, imageHeight);
        Status = $"已加载帧 {key.FrameId}({imageWidth}x{imageHeight})";
    }

    private void RebuildCells(RecognitionFrame? recognition)
    {
        Cells.Clear();
        BoardCells.Clear();
        SideCells.Clear();
        if (recognition is null)
        {
            return;
        }

        for (var i = 0; i < 28; i++)
        {
            var unit = i < recognition.BoardCells.Count ? recognition.BoardCells[i] : null;
            var cell = new ReviewCellDisplay(
                RegionTypes.BoardHero, i, "棋盘",
                unit?.Name ?? string.Empty, unit?.Star ?? 0, UnitTier(unit),
                FormatItems(unit), unit?.Confidence ?? 0.0);
            Cells.Add(cell);
            BoardCells.Add(cell);
        }

        for (var i = 0; i < 9; i++)
        {
            var unit = i < recognition.BenchCells.Count ? recognition.BenchCells[i] : null;
            var cell = new ReviewCellDisplay(
                RegionTypes.BenchHero, i, "备战席",
                unit?.Name ?? string.Empty, unit?.Star ?? 0, UnitTier(unit),
                FormatItems(unit), unit?.Confidence ?? 0.0);
            Cells.Add(cell);
            SideCells.Add(cell);
        }

        for (var i = 0; i < 5; i++)
        {
            var card = i < recognition.ShopCards.Count ? recognition.ShopCards[i] : null;
            string name = card?.Name ?? string.Empty;
            string label = card is { Cost: > 0 } ? $"{name} ({card.Cost}费)" : name;
            var cell = new ReviewCellDisplay(
                RegionTypes.ShopHero, i, "商店", label, 0,
                card?.SourceTier.ToString() ?? string.Empty,
                null, card?.Confidence ?? 0.0);
            Cells.Add(cell);
            SideCells.Add(cell);
        }
    }

    private void RebuildBoxes(int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            Boxes = Array.Empty<ReviewBoxDisplay>();
            return;
        }

        double renderScale = (double)RenderWidth / imageWidth;
        var geometry = BoardGeometry.CreateCanonical();
        var boxes = new List<ReviewBoxDisplay>(42);

        double physicalScale = Math.Min(
            (double)imageWidth / CanonicalRoiDefinitions.CanonicalWidth,
            (double)imageHeight / CanonicalRoiDefinitions.CanonicalHeight);
        double radius = CanonicalRoiDefinitions.BoardHexRadius * physicalScale * renderScale;

        foreach (var cell in geometry.BoardCells)
        {
            var center = _roiMapper.MapPointToPhysical(new Point2d(cell.X, cell.Y), imageWidth, imageHeight);
            double x = center.X * renderScale - radius;
            double y = center.Y * renderScale - radius;
            boxes.Add(new ReviewBoxDisplay(RegionTypes.BoardHero, cell.Row * 7 + cell.Col, $"棋{cell.Row * 7 + cell.Col}", x, y, radius * 2, radius * 2));
        }

        var benchRect = _roiMapper.MapToPhysical(CanonicalRoiDefinitions.BenchBounds, imageWidth, imageHeight);
        double benchW = (double)benchRect.Width / 9.0 * renderScale;
        double benchH = (double)benchRect.Height * renderScale;
        foreach (var cell in geometry.BenchCells)
        {
            var center = _roiMapper.MapPointToPhysical(new Point2d(cell.X, cell.Y), imageWidth, imageHeight);
            boxes.Add(new ReviewBoxDisplay(RegionTypes.BenchHero, cell.Index, $"备{cell.Index}", center.X * renderScale - benchW / 2, center.Y * renderScale - benchH / 2, benchW, benchH));
        }

        var shopRect = _roiMapper.MapToPhysical(CanonicalRoiDefinitions.ShopBounds, imageWidth, imageHeight);
        double shopW = (double)shopRect.Width / 5.0 * renderScale;
        double shopH = (double)shopRect.Height * renderScale;
        foreach (var cell in geometry.ShopCells)
        {
            var center = _roiMapper.MapPointToPhysical(new Point2d(cell.X, cell.Y), imageWidth, imageHeight);
            boxes.Add(new ReviewBoxDisplay(RegionTypes.ShopHero, cell.Index, $"店{cell.Index}", center.X * renderScale - shopW / 2, center.Y * renderScale - shopH / 2, shopW, shopH));
        }

        Boxes = boxes;
    }

    private static string UnitTier(UnitCell? unit) => unit?.SourceTier.ToString() ?? string.Empty;

    /// <summary>把修正类型下拉索引钳制到有效范围(越界时回退分类错)。</summary>
    private int ClampCorrectionTypeIndex()
        => SelectedCorrectionTypeIndex >= 0 && SelectedCorrectionTypeIndex < CorrectionTypeCodes.Count
            ? SelectedCorrectionTypeIndex
            : 0;

    /// <summary>装备展示: iconId 或 iconId×count(无装备返回空)。</summary>
    private static IReadOnlyList<string> FormatItems(UnitCell? unit)
    {
        if (unit?.Items is null || unit.Items.Count == 0)
        {
            return Array.Empty<string>();
        }

        return unit.Items
            .Where(static item => item is not null && !string.IsNullOrWhiteSpace(item.IconId))
            .Select(static item => item.Count > 1 ? $"{item.IconId}×{item.Count}" : item.IconId)
            .ToList();
    }

    private static long SnapshotVersionOf(ReviewFrame? frame)
    {
        if (frame is not null && frame.Meta.TryGetValue("snapshotVersion", out var value) && long.TryParse(value, out var version))
        {
            return version;
        }

        return 0;
    }

    private static Bitmap? MatToBitmap(Mat frame)
    {
        if (frame is null || frame.Empty())
        {
            return null;
        }

        try
        {
            var png = frame.ImEncode(".png");
            return new Bitmap(new MemoryStream(png));
        }
        catch
        {
            // 无 Avalonia 渲染平台(headless 单测)或解码失败时降级为无图。
            return null;
        }
    }
}