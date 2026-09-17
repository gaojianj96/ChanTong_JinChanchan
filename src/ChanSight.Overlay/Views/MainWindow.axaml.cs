using Avalonia.Controls;
using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
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
    private readonly IWindowFinder? _windowFinder;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        LiveView liveView,
        ReviewView reviewView,
        LiveRecognitionService recognition,
        IScreenCaptureService capture,
        IWindowFinder windowFinder) : this()
    {
        DataContext = viewModel;
        _viewModel = viewModel;
        _liveView = liveView;
        _reviewView = reviewView;
        _recognition = recognition;
        _capture = capture;
        _windowFinder = windowFinder;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Host.Content = _liveView;
        Opened += OnOpened;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsReviewMode))
        {
            return;
        }

        Host.Content = (_viewModel?.IsReviewMode ?? false) ? _reviewView : _liveView;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        Opened -= OnOpened;

        await StartCaptureAsync();
    }

    private async System.Threading.Tasks.Task StartCaptureAsync()
    {
        if (_capture is null || _recognition is null || _windowFinder is null)
        {
            return;
        }

        try
        {
            var windows = await _windowFinder.FindWindowsAsync(new WindowSearchOptions(), CancellationToken.None);
            var target = windows.FirstOrDefault();
            if (target is null)
            {
                return;
            }

            await _capture.StartAsync(target);
            _recognition.Start();
        }
        catch
        {
            // 找不到游戏窗口或捕获失败时不阻断 UI; 用户仍可手动操作。
        }
    }
}