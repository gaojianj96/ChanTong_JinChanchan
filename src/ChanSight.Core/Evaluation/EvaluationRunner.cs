using ChanSight.Core.Annotation;

namespace ChanSight.Core.Evaluation;

/// <summary>
/// 金标评测器: 读有效金标(Revoked 已过滤), 逐一比对识别值 vs 金标答案, 产出分维度准确率/混淆报告。
/// 口径: 每条金标都是"应有答案"。识别值与金标语义等价 → Tp(正确);
/// 金标为空但识别有值 → Fp(误检); 金标有值但识别为空/不等 → Fn(漏检或分类错);
/// 识别值与金标都空 → 记入 <see cref="EvaluationReport.Issues"/> 且不进入混淆。
/// </summary>
public sealed class EvaluationRunner
{
    private const string Untyped = "(untyped)";

    private readonly AnnotationStore _store;

    public EvaluationRunner(AnnotationStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<EvaluationReport> EvaluateAsync(string matchId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);

        var labels = await _store.LoadGoldLabelsAsync(matchId, ct).ConfigureAwait(false);
        return Evaluate(labels);
    }

    /// <summary>同步纯内存评测。防御性排除空引用与 Revoked 金标(便于直接用原始列表测试)。</summary>
    public static EvaluationReport Evaluate(IReadOnlyList<GoldLabel> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        var valid = new List<GoldLabel>(labels.Count);
        foreach (var label in labels)
        {
            if (label is not null && !label.Revoked)
            {
                valid.Add(label);
            }
        }

        var issues = new List<string>();
        int totalCorrect = 0;

        var regionBuckets = new Dictionary<string, ConfusionAccumulator>(StringComparer.Ordinal);
        var tierBuckets = new Dictionary<string, ConfusionAccumulator>(StringComparer.Ordinal);
        var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var label in valid)
        {
            CountType(typeCounts, label.CorrectionType);

            bool emptyRecognized = CorrectionValue.IsEmpty(label.RecognizedValue);
            bool emptyCorrected = CorrectionValue.IsEmpty(label.CorrectedValue);

            if (emptyRecognized && emptyCorrected)
            {
                issues.Add($"Label '{label.Id}': RecognizedValue and CorrectedValue are both empty; excluded from confusion.");
                continue;
            }

            var (correct, kind) = Classify(label, emptyRecognized, emptyCorrected);
            if (correct)
            {
                totalCorrect++;
            }

            Accumulate(regionBuckets, label.RegionType, kind);
            Accumulate(tierBuckets, label.SourceTier, kind);
        }

        int total = valid.Count;
        double overallAccuracy = total == 0 ? 0.0 : (double)totalCorrect / total;

        return new EvaluationReport(
            total,
            totalCorrect,
            overallAccuracy,
            regionBuckets
                .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
                .Select(static kv => new RegionMetric(kv.Key, kv.Value.ToConfusion()))
                .ToList(),
            tierBuckets
                .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
                .Select(static kv => new TierMetric(kv.Key, kv.Value.ToConfusion()))
                .ToList(),
            typeCounts
                .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
                .Select(static kv => new TypeMetric(kv.Key, kv.Value))
                .ToList(),
            issues);
    }

    private static (bool Correct, Bucket Kind) Classify(GoldLabel label, bool emptyRecognized, bool emptyCorrected)
    {
        if (emptyCorrected)
        {
            // 金标为空但识别有值 → 误检(false-positive)。
            return (false, Bucket.Fp);
        }

        if (emptyRecognized)
        {
            // 金标有值但识别为空 → 漏检(missed)。
            return (false, Bucket.Fn);
        }

        bool equal = CorrectionValue.Equals(label.RecognizedValue, label.CorrectedValue);
        return equal ? (true, Bucket.Tp) : (false, Bucket.Fn);
    }

    private static void CountType(Dictionary<string, int> counts, string? correctionType)
    {
        var key = correctionType ?? Untyped;
        counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private static void Accumulate(Dictionary<string, ConfusionAccumulator> buckets, string key, Bucket kind)
    {
        if (!buckets.TryGetValue(key, out var accumulator))
        {
            accumulator = new ConfusionAccumulator();
            buckets[key] = accumulator;
        }

        accumulator.Add(kind);
    }

    private enum Bucket
    {
        Tp,
        Fp,
        Tn,
        Fn
    }

    private sealed class ConfusionAccumulator
    {
        private int _tp;
        private int _fp;
        private int _tn;
        private int _fn;

        public void Add(Bucket kind)
        {
            switch (kind)
            {
                case Bucket.Tp:
                    _tp++;
                    break;
                case Bucket.Fp:
                    _fp++;
                    break;
                case Bucket.Tn:
                    _tn++;
                    break;
                case Bucket.Fn:
                    _fn++;
                    break;
            }
        }

        public Confusion ToConfusion() => new(_tp, _fp, _tn, _fn);
    }
}