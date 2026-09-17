using Avalonia.Controls;
using ChanSight.Overlay.ViewModels;

namespace ChanSight.Overlay.Views;

public partial class LiveView : UserControl
{
    public LiveView()
    {
        InitializeComponent();
    }

    public LiveView(LiveViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}