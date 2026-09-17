namespace ChanSight.Overlay.Models;

/// <summary>
/// 回顾窗口的单格决策: 用户对某帧某格(RegionType + CellIndex)的最终判断。
/// CorrectionType 为 null 时表示未修正(由 <see cref="Services.ReviewService"/> 自动补 confirm-correct);
/// CorrectionId 关联实时修正(<see cref="ChanSight.Core.Annotation.CorrectionRecord"/>)记录, 可 null。
/// </summary>
public sealed record ReviewDecision(
    string RegionType,
    int CellIndex,
    string? RecognizedValue,
    string CorrectedValue,
    string? CorrectionType,
    string? CorrectionId);