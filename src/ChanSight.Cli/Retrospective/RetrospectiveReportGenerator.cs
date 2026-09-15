using System.Globalization;
using System.Text;
using System.Text.Json;
using ChanSight.Recorder.Services;
using ChanSight.Vision.Interfaces;

namespace ChanSight.Cli.Retrospective;

/// <summary>
/// Turns R2's algorithmic score + the raw event stream into an evidence-cited
/// natural-language post-match report via a single <see cref="IVlmClient"/> text
/// completion. Every finding must cite concrete event Seqs; uncited assertions
/// are rejected. A fixed per-instance call budget bounds LLM spend.
/// </summary>
public sealed class RetrospectiveReportGenerator
{
    private readonly IVlmClient _client;
    private readonly RetrospectiveScorer _scorer;
    private readonly int _maxCalls;
    private int _callCount;

    public RetrospectiveReportGenerator(
        IVlmClient client,
        RetrospectiveScorer scorer,
        int maxCalls = Budget.MaxCalls)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
        if (maxCalls <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCalls));

        _maxCalls = maxCalls;
    }

    public async Task<RetrospectiveReport> GenerateAsync(
        IReadOnlyList<MatchEvent> events,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (Interlocked.Increment(ref _callCount) > _maxCalls)
            throw new InvalidOperationException("report budget exceeded");

        var score = _scorer.Score(events);
        var prompt = BuildPrompt(score, events);

        var raw = await _client
            .CompleteAsync(prompt, Array.Empty<(string mime, byte[] data)>(), ct)
            .ConfigureAwait(false);

        return Parse(raw, events);
    }

    private static RetrospectiveReport Parse(string raw, IReadOnlyList<MatchEvent> events)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Report returned malformed JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Report response must be a JSON object.");

            var summary = RequireNonEmptyString(root, "summary");
            var findings = RequireArray(root, "findings");
            var nextActions = RequireArray(root, "nextActions");
            var confidence = RequireConfidence(root, "confidence");

            var validSeqs = new HashSet<long>(events.Select(e => e.Seq));

            var parsedFindings = new List<RetrospectiveFinding>(findings.GetArrayLength());
            foreach (var finding in findings.EnumerateArray())
                parsedFindings.Add(ParseFinding(finding, validSeqs));

            var parsedNextActions = new List<string>(nextActions.GetArrayLength());
            foreach (var action in nextActions.EnumerateArray())
            {
                if (action.ValueKind != JsonValueKind.String)
                    throw new InvalidOperationException("Report 'nextActions' entries must be strings.");

                parsedNextActions.Add(action.GetString()!);
            }

            return new RetrospectiveReport(
                summary,
                parsedFindings.AsReadOnly(),
                parsedNextActions.AsReadOnly(),
                confidence);
        }
    }

    private static RetrospectiveFinding ParseFinding(JsonElement element, HashSet<long> validSeqs)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Report 'findings' entries must be JSON objects.");

        var claim = RequireNonEmptyString(element, "claim");
        var confidence = RequireConfidence(element, "confidence");

        if (!element.TryGetProperty("evidenceSeqs", out var seqsElement)
            || seqsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Report finding is missing a valid 'evidenceSeqs' field.");
        }

        var seqs = new List<long>(seqsElement.GetArrayLength());
        foreach (var seq in seqsElement.EnumerateArray())
        {
            if (seq.ValueKind != JsonValueKind.Number || !seq.TryGetInt64(out var value))
                throw new InvalidOperationException("Report finding 'evidenceSeqs' entries must be integers.");

            seqs.Add(value);
        }

        if (seqs.Count == 0 || seqs.Any(seq => !validSeqs.Contains(seq)))
            throw new InvalidOperationException("finding without evidence");

        return new RetrospectiveFinding(claim, seqs.AsReadOnly(), confidence);
    }

    private static string RequireNonEmptyString(JsonElement obj, string field)
    {
        if (!obj.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Report is missing a valid '{field}' field.");

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"Report '{field}' must not be empty.");

        return text!;
    }

    private static double RequireConfidence(JsonElement obj, string field)
    {
        if (!obj.TryGetProperty(field, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var confidence))
        {
            throw new InvalidOperationException($"Report is missing a valid '{field}' field.");
        }

        if (confidence < 0.0 || confidence > 1.0)
            throw new InvalidOperationException($"Report '{field}' must be within [0, 1], got {confidence}.");

        return confidence;
    }

    private static JsonElement RequireArray(JsonElement obj, string field)
    {
        if (!obj.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Report is missing a valid '{field}' field.");

        return value;
    }

    private static string BuildPrompt(RetrospectiveScore score, IReadOnlyList<MatchEvent> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是《金铲铲之战》赛后复盘解说员。请基于给定的算法评分(RetrospectiveScore)与原始事件流, 产出一份证据引用式复盘报告(Retrospective)。");
        builder.AppendLine();
        builder.AppendLine("硬性要求:");
        builder.AppendLine("1. 每一条结论(findings 中的每一项)都必须通过 evidenceSeqs 引用真实存在的事件序号(Seq), 禁止输出无法指认事件序号的无据主张;");
        builder.AppendLine("2. 只输出一个 JSON 对象, 不要输出任何解释、前缀或 markdown 代码块。");
        builder.AppendLine();
        builder.AppendLine("算法评分(RetrospectiveScore):");
        builder.AppendLine($"- 最终局面(Factual): 阶段={score.Factual.FinalStage}, 血量={score.Factual.FinalHp}, 结果={score.Factual.Outcome}");
        builder.AppendLine($"- 决策期望(Expectation): 决策数={score.Expectation.DecisionCount}, 平均风险={Format(score.Expectation.AverageRisk)}, 期望置信度={Format(score.Expectation.ConfidenceScore)}");
        builder.AppendLine($"- 执行质量(Execution): 有动作决策={score.Execution.DecisionsWithAction}/{score.Execution.TotalDecisions}, 执行率={Format(score.Execution.Rate)}");
        builder.AppendLine("- 运气(Luck):");
        foreach (var luck in score.Luck)
            builder.AppendLine($"  - {luck.Label}(严重度 {Format(luck.Severity)}): {string.Join("; ", luck.Evidence)}");
        builder.AppendLine("- 问题(Issues):");
        foreach (var issue in score.Issues)
            builder.AppendLine($"  - {issue}");
        builder.AppendLine();
        builder.AppendLine("事件摘要(按 Seq):");
        foreach (var e in events)
            builder.AppendLine(SummarizeEvent(e));
        builder.AppendLine();
        builder.AppendLine("只输出一个 JSON 对象, schema 如下:");
        builder.AppendLine("""{"summary":"总体结论(非空字符串)","findings":[{"claim":"结论文本(非空字符串)","evidenceSeqs":[1,2],"confidence":0.8}],"nextActions":["下一步行动建议"],"confidence":0.7}""");
        return builder.ToString();
    }

    private static string SummarizeEvent(MatchEvent e)
    {
        const int maxPayload = 200;
        var payload = string.IsNullOrWhiteSpace(e.PayloadJson) ? string.Empty : e.PayloadJson;
        if (payload.Length > maxPayload)
            payload = payload[..maxPayload] + "…";

        return $"- Seq {e.Seq} [{e.Type}] {payload}";
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Fixed per-instance call budget. Once exceeded the generator refuses further
    /// calls to bound LLM spend.
    /// </summary>
    public static class Budget
    {
        public const int MaxCalls = 100;
    }
}