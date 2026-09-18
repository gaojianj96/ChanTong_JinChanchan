using Avalonia.Controls;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.Services;
using ChanSight.Overlay.ViewModels;

namespace ChanSight.Overlay.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel? _viewModel;
    private readonly LiveView? _liveView;
    private readonly ReviewView? _reviewView;
    private readonly LiveRecognitionService? _recognition;
    private readonly IScreenCaptureService? _capture;

    private nint _startedHwnd;
    private long _frameCount;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        LiveView liveView,
        ReviewView reviewView,
        LiveRecognitionService recognition,
        IScreenCaptureService capture) : this()
    {
        DataContext = viewModel;
        _viewModel = viewModel;
        _liveView = liveView;
        _reviewView = reviewView;
        _recognition = recognition;
        _capture = capture;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _recognition.FrameUpdated += OnFrameUpdated;
        Host.Content = _liveView;
        Opened += OnOpened;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsReviewMode))
        {
            Host.Content = (_viewModel?.IsReviewMode ?? false) ? _reviewView : _liveView;
            return;
        }

        // 下拉框选择变化 → 自动(重新)开始捕获目标窗口。
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedWindowIndex))
        {
            _ = RestartCaptureAsync();
        }
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        Opened -= OnOpened;

        // 扫描窗口并填充下拉框: 单个匹配自动选中(触发捕获), 多个等待选择, 找不到展示引导文案。
        if (_viewModel is not null)
        {
            await _viewModel.RefreshWindowsAsync();
        }
        else
        {
            await StartCaptureAsync();
        }
    }

    private async System.Threading.Tasks.Task StartCaptureAsync()
    {
        if (_capture is null || _recognition is null || _viewModel is null)
        {
            return;
        }

        var target = _viewModel.SelectedTarget;

        // 未选择(可能因尚未扫描或扫描结果为空) → 尝试重新扫描以便给出明确状态。
        if (target is null)
        {
            await _viewModel.RefreshWindowsAsync();
            target = _viewModel.SelectedTarget;
        }

        if (target is null)
        {
            _viewModel.Status = _viewModel.HasWindows
                ? "请在窗口下拉框中选择目标窗口"
                : "未找到游戏窗口";
            return;
        }

        await RestartCaptureAsync();
    }

    private async System.Threading.Tasks.Task RestartCaptureAsync()
    {
        if (_capture is null || _recognition is null || _viewModel is null)
        {
            return;
        }

        var target = _viewModel.SelectedTarget;
        if (target is null)
        {
            return;
        }

        // 已针对同一窗口捕获时跳过(避免重复 StartAsync 抛 already-running)。
        if (_capture.IsRunning && _startedHwnd == target.Hwnd)
        {
            return;
        }

        try
        {
            if (_capture.IsRunning)
            {
                await _capture.StopAsync();
            }

            await _capture.StartAsync(target);
            _startedHwnd = target.Hwnd;

            if (!_recognition.IsRunning)
            {
                _recognition.Start();
            }

            _viewModel.Status = "捕获中…";
        }
        catch (Exception ex)
        {
            _viewModel.Status = $"捕获失败: {ex.Message}";
        }
    }

    private void OnFrameUpdated(LiveFrameUpdate update)
    {
        if (_viewModel is null)
        {
            return;
        }

        _frameCount++;

        if (_viewModel.IsReviewMode)
        {
            return;
        }

        _viewModel.Status = $"识别中… 已捕获 {_frameCount} 帧 · 阶段 {update.State.Stage}";
    }
}