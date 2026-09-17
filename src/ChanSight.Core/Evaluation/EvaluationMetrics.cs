namespace ChanSight.Core.Evaluation;

/// <summary>
/// 二分类混淆矩阵(按维度聚合)。分母为 0 时对应比率返回 0。
/// </summary>
public sealed record Confusion(int Tp, int Fp, int Tn, int Fn)
{
    private int Total => Tp + Fp + Tn + Fn;

    public double Accuracy => Total == 0 ? 0.0 : (double)(Tp + Tn) / Total;

    public double Precision => Tp + Fp == 0 ? 0.0 : (double)Tp / (Tp + Fp);

    public double Recall => Tp + Fn == 0 ? 0.0 : (double)Tp / (Tp + Fn);
}

/// <summary>按区域(<see cref="GoldLabel.RegionType"/>)聚合的混淆。</summary>
public sealed record RegionMetric(string RegionType, Confusion Confusion);

/// <summary>按来源层级(<see cref="GoldLabel.SourceTier"/>)聚合的混淆。</summary>
public sealed record TierMetric(string SourceTier, Confusion Confusion);

/// <summary>按修正类型(<see cref="GoldLabel.CorrectionType"/>)的计数分布。</summary>
public sealed record TypeMetric(string CorrectionType, int Count);

/// <summary>
/// 金标评测报告: 离线读金标集, 比对识别值 vs 金标答案后产出的分维度指标。
/// 可直接经 <c>System.Text.Json</c> 序列化存档。
/// </summary>
public sealed record EvaluationReport(
    int TotalLabels,
    int TotalCorrect,
    double OverallAccuracy,
    IReadOnlyList<RegionMetric> ByRegion,
    IReadOnlyList<TierMetric> ByTier,
    IReadOnlyList<TypeMetric> ByType,
    IReadOnlyList<string> Issues);