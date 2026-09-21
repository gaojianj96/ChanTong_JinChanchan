using Avalonia.Controls;
using ChanSight.Overlay.ViewModels;

namespace ChanSight.Overlay.Views;

public partial class DictionaryView : UserControl
{
    public DictionaryView()
    {
        InitializeComponent();
    }

    public DictionaryView(DictionaryViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>视图打开时触发刷新, 从 SeasonRuntime 重读当前赛季字典。</summary>
    public void Refresh()
    {
        if (DataContext is DictionaryViewModel viewModel)
        {
            viewModel.Refresh();
        }
    }
}