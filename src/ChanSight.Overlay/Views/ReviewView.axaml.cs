using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ChanSight.Overlay.ViewModels;

namespace ChanSight.Overlay.Views;

public partial class ReviewView : UserControl
{
    private bool _isPanning;
    private Point _panStartPointer;
    private Vector _panStartOffset;

    public ReviewView()
    {
        InitializeComponent();
    }

    public ReviewView(ReviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnImagePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not ReviewViewModel vm)
        {
            return;
        }

        vm.ZoomBy(e.Delta.Y > 0 ? 1.25 : 0.8);
        e.Handled = true;
    }

    private void OnImagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _panStartPointer = e.GetPosition(ImageScroll);
        _panStartOffset = ImageScroll.Offset;
        e.Pointer.Capture(ImageScroll);
        e.Handled = true;
    }

    private void OnImagePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var current = e.GetPosition(ImageScroll);
        var delta = _panStartPointer - current;
        ImageScroll.Offset = _panStartOffset + delta;
    }

    private void OnImagePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        e.Pointer.Capture(null);
    }

    private void OnImagePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPanning = false;
    }
}