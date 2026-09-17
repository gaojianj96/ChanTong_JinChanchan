namespace ChanSight.Overlay.ViewModels;

/// <summary>
/// 棋盘 4×7 六角网格的 UI 布局常量(仅用于把 SlotIndex 映射为交错排列的像素坐标)。
/// 不参与任何识别/业务逻辑, 只服务于棋盘格的视觉摆放。
/// </summary>
public static class BoardGridLayout
{
    /// <summary>同一行相邻格中心点的水平间距(像素)。</summary>
    public const double PitchX = 76;

    /// <summary>相邻行中心点的垂直间距(像素)。</summary>
    public const double PitchY = 46;

    /// <summary>奇数行相对偶数行的水平错位量(模拟六角交错的半个步距)。</summary>
    public const double Stagger = 38;
}