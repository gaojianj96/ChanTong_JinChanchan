using Avalonia.Controls;
using ChanSight.Overlay.ViewModels;

namespace ChanSight.Overlay.Views;

public partial class ReviewView : UserControl
{
    public ReviewView()
    {
        InitializeComponent();
    }

    public ReviewView(ReviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}