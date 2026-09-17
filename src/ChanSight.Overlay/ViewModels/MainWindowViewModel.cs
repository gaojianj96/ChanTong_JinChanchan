using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChanSight.Overlay.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = "ChanSight Overlay";

    [ObservableProperty]
    private bool _isReviewMode;

    [ObservableProperty]
    private string _toggleLabel = "切换到回顾窗口";

    partial void OnIsReviewModeChanged(bool value)
    {
        ToggleLabel = value ? "切换到实时识别" : "切换到回顾窗口";
        Status = value ? "回顾窗口(帧回放 + 原图叠框 + 金标)" : "ChanSight Overlay";
    }

    [RelayCommand]
    private void ToggleMode() => IsReviewMode = !IsReviewMode;
}