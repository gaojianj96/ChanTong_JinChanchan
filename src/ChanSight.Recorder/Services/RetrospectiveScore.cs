namespace ChanSight.Recorder.Services;

public enum MatchOutcome
{
    Win,
    Loss,
    Unknown
}

public sealed record FactualResult(string FinalStage, int FinalHp, MatchOutcome Outcome);

public sealed record DecisionExpectation(int DecisionCount, double AverageRisk, double ConfidenceScore);

public sealed record ExecutionQuality(int DecisionsWithAction, int TotalDecisions)
{
    public double Rate => TotalDecisions == 0 ? 0 : (double)DecisionsWithAction / TotalDecisions;
}

public sealed record LuckAssessment(string Label, double Severity, IReadOnlyList<string> Evidence);

public sealed record RetrospectiveScore(
    FactualResult Factual,
    DecisionExpectation Expectation,
    ExecutionQuality Execution,
    IReadOnlyList<LuckAssessment> Luck,
    IReadOnlyList<string> Issues);