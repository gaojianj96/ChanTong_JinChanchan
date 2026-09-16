namespace ChanSight.Core.Annotation;

/// <summary>
/// <see cref="GoldLabel.CorrectionType"/> 的字段级字符串枚举约定(可为 null)。
/// </summary>
public static class CorrectionTypes
{
    public const string Missed = "missed";

    public const string FalsePositive = "false-positive";

    public const string Localization = "localization";

    public const string Classification = "classification";

    public const string ConfirmCorrect = "confirm-correct";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Missed,
        FalsePositive,
        Localization,
        Classification,
        ConfirmCorrect
    };

    public static bool IsValid(string? correctionType) => correctionType is null || All.Contains(correctionType);
}