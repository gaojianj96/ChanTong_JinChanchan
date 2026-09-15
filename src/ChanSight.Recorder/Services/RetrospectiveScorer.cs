using System.Text.Json;

namespace ChanSight.Recorder.Services;

public sealed class RetrospectiveScorer
{
    public RetrospectiveScore Score(IReadOnlyList<MatchEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var decisions = new List<DecisionPayload>();
        var actionDecisionIds = new List<string>();
        var issues = new List<string>();

        MatchEvent? lastStageOrSnapshot = null;
        var seenWin = false;
        var seenLoss = false;

        foreach (var e in events)
        {
            switch (e.Type)
            {
                case MatchEventType.StageChange:
                case MatchEventType.Snapshot:
                    lastStageOrSnapshot = e;
                    break;

                case MatchEventType.System:
                    var outcome = ReadOutcome(e.PayloadJson);
                    if (outcome == MatchOutcome.Win)
                        seenWin = true;
                    else if (outcome == MatchOutcome.Loss)
                        seenLoss = true;
                    break;

                case MatchEventType.Decision:
                    var payload = DecisionPayload.TryParse(e.PayloadJson);
                    if (payload is null)
                    {
                        issues.Add($"Decision Seq {e.Seq} 载荷非法");
                    }
                    else
                    {
                        decisions.Add(payload);
                    }
                    break;

                case MatchEventType.Action:
                    if (TryReadDecisionId(e.PayloadJson, out var actionDecisionId))
                    {
                        actionDecisionIds.Add(actionDecisionId);
                    }
                    break;
            }
        }

        var (finalStage, finalHp) = ReadStage(lastStageOrSnapshot);
        var outcomeResult = seenWin ? MatchOutcome.Win : seenLoss ? MatchOutcome.Loss : MatchOutcome.Unknown;

        var decisionCount = decisions.Count;
        double averageRisk = decisionCount == 0 ? 0 : decisions.Average(d => d.Risk);
        double confidenceScore = decisionCount == 0 ? 0 : 1 - averageRisk;

        var decisionIdSet = new HashSet<string>(decisions.Select(d => d.DecisionId), StringComparer.Ordinal);
        var decisionsWithAction = actionDecisionIds.Count(id => decisionIdSet.Contains(id));

        var luck = new List<LuckAssessment>();
        if (!string.IsNullOrEmpty(finalStage) && outcomeResult == MatchOutcome.Unknown)
        {
            luck.Add(new LuckAssessment("对手强度未知", 0.5, new[] { "未记录对局结果，无法评估对手强度" }));
        }

        if (decisionCount > 0 && decisionsWithAction == 0)
        {
            luck.Add(new LuckAssessment("执行缺失", 0.3, new[] { "已解析决策无对应执行动作" }));
        }

        return new RetrospectiveScore(
            new FactualResult(finalStage, finalHp, outcomeResult),
            new DecisionExpectation(decisionCount, averageRisk, confidenceScore),
            new ExecutionQuality(decisionsWithAction, decisionCount),
            luck.AsReadOnly(),
            issues.AsReadOnly());
    }

    private static (string Stage, int Hp) ReadStage(MatchEvent? lastStageOrSnapshot)
    {
        if (lastStageOrSnapshot is null || !TryParseObject(lastStageOrSnapshot.PayloadJson, out var root))
        {
            return (string.Empty, 0);
        }

        var stage = string.Empty;
        var hp = 0;

        if (TryGetString(root, "stage", out var stageValue) && stageValue is not null)
        {
            stage = stageValue;
        }

        if (TryGetInt(root, "hp", out var hpValue))
        {
            hp = hpValue;
        }

        return (stage, hp);
    }

    private static MatchOutcome ReadOutcome(string payloadJson)
    {
        if (!TryParseObject(payloadJson, out var root))
        {
            return MatchOutcome.Unknown;
        }

        if (TryGetString(root, "outcome", out var value))
        {
            if (string.Equals(value, "win", StringComparison.OrdinalIgnoreCase))
                return MatchOutcome.Win;
            if (string.Equals(value, "loss", StringComparison.OrdinalIgnoreCase))
                return MatchOutcome.Loss;
        }

        return MatchOutcome.Unknown;
    }

    private static bool TryReadDecisionId(string payloadJson, out string decisionId)
    {
        if (TryParseObject(payloadJson, out var root) && TryGetString(root, "decisionId", out var value))
        {
            decisionId = value ?? string.Empty;
            return true;
        }

        decisionId = string.Empty;
        return false;
    }

    private static bool TryParseObject(string payloadJson, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (FindProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        if (FindProperty(element, propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool FindProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}