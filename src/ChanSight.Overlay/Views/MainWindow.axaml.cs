using Avalonia.Controls;
using ChanSight.Overlay.ViewModels;

namespace ChanSight.Overlay.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
