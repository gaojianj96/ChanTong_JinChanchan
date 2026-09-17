using CommunityToolkit.Mvvm.ComponentModel;

namespace ChanSight.Overlay.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = "ChanSight Overlay";
}
