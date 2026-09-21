using System.Collections.ObjectModel;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChanSight.Overlay.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IWindowFinder? _windowFinder;

    [ObservableProperty]
    private string _status = "等待识别…";

    [ObservableProperty]
    private bool _isReviewMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DictionaryLabel))]
    private bool _isDictionaryMode;

    [ObservableProperty]
    private string _toggleLabel = "切换到回顾窗口";

    [ObservableProperty]
    private bool _hasWindows;

    [ObservableProperty]
    private int _selectedWindowIndex = -1;

    /// <summary>匹配窗口标题的可读列表, 格式对齐老 CLI: "标题 [WxH]"。与 <see cref="_targets"/> 下标一一对应。</summary>
    public ObservableCollection<string> WindowOptions { get; } = new();

    /// <summary>未找到窗口时的引导文案(含关键词)。</summary>
    public string NoWindowHint { get; } =
        "未找到匹配窗口, 请确认游戏/模拟器已启动, 关键词: 金铲铲之战/MuMu/雷电/夜神/Tencent";

    private IReadOnlyList<WindowTarget> _targets = Array.Empty<WindowTarget>();

    public MainWindowViewModel()
    {
    }

    public MainWindowViewModel(IWindowFinder windowFinder)
    {
        _windowFinder = windowFinder ?? throw new ArgumentNullException(nameof(windowFinder));
    }

    /// <summary>当前下拉框所选窗口; 未选或越界返回 null。</summary>
    public WindowTarget? SelectedTarget =>
        SelectedWindowIndex >= 0 && SelectedWindowIndex < _targets.Count
            ? _targets[SelectedWindowIndex]
            : null;

    partial void OnIsReviewModeChanged(bool value)
    {
        if (value)
        {
            IsDictionaryMode = false;
        }

        ToggleLabel = value ? "切换到实时识别" : "切换到回顾窗口";
        Status = value ? "回顾窗口(帧回放 + 原图叠框 + 金标)" : "等待识别…";
    }

    /// <summary>词典视图入口按钮文案。</summary>
    public string DictionaryLabel => IsDictionaryMode ? "退出词典" : "打开词典";

    partial void OnIsDictionaryModeChanged(bool value)
    {
        if (value)
        {
            IsReviewMode = false;
        }

        Status = value ? "字典查看(奕子/装备/羁绊/prompt/候选/阵容)" : "等待识别…";
    }

    [RelayCommand]
    private void ToggleMode() => IsReviewMode = !IsReviewMode;

    [RelayCommand]
    private void ToggleDictionary() => IsDictionaryMode = !IsDictionaryMode;

    /// <summary>
    /// 重新扫描匹配窗口并填充 <see cref="WindowOptions"/>. 仅 1 个匹配时自动选中; 多个时置为未选等待用户选择。
    /// 找不到窗口时清空列表并把 <see cref="HasWindows"/> 置 false, 由视图展示引导文案。
    /// </summary>
    [RelayCommand]
    public async Task RefreshWindowsAsync()
    {
        if (_windowFinder is null)
        {
            throw new InvalidOperationException("Window finder is not configured.");
        }

        var targets = await _windowFinder.FindWindowsAsync(new WindowSearchOptions(), CancellationToken.None);

        _targets = targets;
        WindowOptions.Clear();
        foreach (var target in targets)
        {
            WindowOptions.Add($"{target.Title} [{target.PhysicalSize.Width}x{target.PhysicalSize.Height}]");
        }

        HasWindows = targets.Count > 0;
        SelectedWindowIndex = targets.Count == 1 ? 0 : -1;

        Status = targets.Count switch
        {
            0 => "未找到游戏窗口",
            1 => "已找到 1 个窗口, 准备捕获…",
            _ => $"找到 {targets.Count} 个窗口, 请在下拉框选择目标",
        };
    }
}