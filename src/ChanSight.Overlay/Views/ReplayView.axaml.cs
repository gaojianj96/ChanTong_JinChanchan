using Avalonia.Controls;
using ChanSight.Overlay.ViewModels;

namespace ChanSight.Overlay.Views;

public partial class ReplayView : UserControl
{
    public ReplayView()
    {
        InitializeComponent();
    }

    public ReplayView(ReplayViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}