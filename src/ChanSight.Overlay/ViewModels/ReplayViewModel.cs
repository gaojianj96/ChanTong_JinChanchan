using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using ChanSight.Core.Annotation;
using ChanSight.Core.FrameStorage;
using ChanSight.Overlay.Services;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace ChanSight.Overlay.ViewModels;

/// <summary>
/// 回放重模拟窗口 ViewModel: 列出关键帧时间轴 → 选中帧直读识别结果(不自动调 VLM) →
/// 未识别帧显示「未识别」占位 → 「重新识别此帧」手动触发 VLM 并刷新该帧展示。
/// </summary>
public partial class ReplayViewModel : ObservableObject
{
    private const double MaxRenderWidth = 440.0;

    private readonly ReplaySimService _service;
    private readonly IRoiMapperService _roiMapper;

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
    private double _renderWidth;

    [ObservableProperty]
    private double _renderHeight;

    [ObservableProperty]
    private bool _hasRecognition;

    [ObservableProperty]
    private string _recognitionSummary = "未识别";

    [ObservableProperty]
    private bool _isSelf = true;

    [ObservableProperty]
    private int _perspectiveIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private IReadOnlyList<ReviewBoxDisplay> _boxes = Array.Empty<ReviewBoxDisplay>();

    public ObservableCollection<ReviewCellDisplay> Cells { get; } = new();

    public IReadOnlyList<string> PerspectiveOptions { get; } = ["己方", "对手"];

    public ReplayViewModel(ReplaySimService service, IRoiMapperService? roiMapper = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _roiMapper = roiMapper ?? new RoiMapperService();
    }

    partial void OnPerspectiveIndexChanged(int value) => IsSelf = value == 0;

    [RelayCommand]
    private void LoadFrames()
    {
        if (string.IsNullOrWhiteSpace(MatchId))
        {
            Status = "对局 ID 不能为空";
            return;
        }

        KeyFrames = _service.ListFrames(MatchId);
        SelectedFrame = null;
        Cells.Clear();
        Boxes = Array.Empty<ReviewBoxDisplay>();
        FrameBitmap = null;
        HasRecognition = false;
        RecognitionSummary = "未识别";
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

    /// <summary>「重新识别此帧」: 手动触发 VLM(己方/对手视角由 IsSelf 决定), 完成后刷新该帧展示。</summary>
    [RelayCommand]
    private async Task ReRecognizeSelectedAsync()
    {
        if (SelectedFrame is null)
        {
            Status = "请先选择关键帧";
            return;
        }

        IsBusy = true;
        Status = "VLM 重识别中…";
        try
        {
            var recognition = await _service.ReRecognizeAsync(MatchId, SelectedFrame.FrameId, IsSelf, CancellationToken.None)
                .ConfigureAwait(true);

            HasRecognition = true;
            RebuildRecognitionDisplay(recognition);
            Status = $"已重识别帧 {SelectedFrame.FrameId}({(IsSelf ? "己方" : "对手")} 视角), 结果已写回缓存";
        }
        catch (Exception ex)
        {
            Status = $"重识别失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadSelectedFrame(FrameKey key)
    {
        var replay = _service.Load(MatchId, key.FrameId);
        try
        {
            int imageWidth = replay.FrameImage.Cols;
            int imageHeight = replay.FrameImage.Rows;

            FrameBitmap = MatToBitmap(replay.FrameImage);

            RenderWidth = Math.Min(MaxRenderWidth, imageWidth);
            RenderHeight = imageWidth > 0 ? RenderWidth * imageHeight / imageWidth : 0;

            RebuildBoxes(imageWidth, imageHeight);
            HasRecognition = replay.HasRecognition;
            RebuildRecognitionDisplay(replay.Recognition);

            Status = replay.HasRecognition
                ? $"已加载帧 {key.FrameId}(直读缓存识别结果)"
                : $"已加载帧 {key.FrameId}(未识别)";
        }
        finally
        {
            replay.FrameImage.Dispose();
        }
    }

    private void RebuildRecognitionDisplay(RecognitionFrame? recognition)
    {
        Cells.Clear();
        if (recognition is null)
        {
            RecognitionSummary = "未识别 — 点击「重新识别此帧」触发 VLM";
            return;
        }

        string stage = string.IsNullOrWhiteSpace(recognition.Stage) ? "?" : recognition.Stage;
        string issues = recognition.Issues.Count > 0 ? $" · {recognition.Issues.Count} 条问题" : string.Empty;
        RecognitionSummary =
            $"阶段 {stage} · 金币 {recognition.Gold} · 等级 {recognition.Level} · 血量 {recognition.Hp} · 置信度 {recognition.Confidence:P0}{issues}";

        for (var i = 0; i < 28; i++)
        {
            var unit = i < recognition.BoardCells.Count ? recognition.BoardCells[i] : null;
            Cells.Add(new ReviewCellDisplay(RegionTypes.BoardHero, i, "棋盘", unit?.Name ?? string.Empty, unit?.Star ?? 0, UnitTier(unit)));
        }

        for (var i = 0; i < 9; i++)
        {
            var unit = i < recognition.BenchCells.Count ? recognition.BenchCells[i] : null;
            Cells.Add(new ReviewCellDisplay(RegionTypes.BenchHero, i, "备战席", unit?.Name ?? string.Empty, unit?.Star ?? 0, UnitTier(unit)));
        }

        for (var i = 0; i < 5; i++)
        {
            var card = i < recognition.ShopCards.Count ? recognition.ShopCards[i] : null;
            string name = card?.Name ?? string.Empty;
            string label = card is { Cost: > 0 } ? $"{name} ({card.Cost}费)" : name;
            Cells.Add(new ReviewCellDisplay(RegionTypes.ShopHero, i, "商店", label, 0, card?.SourceTier.ToString() ?? string.Empty));
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
            return null;
        }
    }
}