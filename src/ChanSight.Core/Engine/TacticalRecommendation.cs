namespace ChanSight.Core.Engine;

public enum AdviceKind
{
    CompRecommend,
    RollDecision,
    EconomyDecision,
}

public enum Verdict
{
    LevelUp,
    Roll,
    SmallRoll,
    Stop,
    Lock,
    Hold,
    Adopt,
}

public sealed record TacticalRecommendation(
    AdviceKind Kind,
    Verdict Verdict,
    int Priority,
    string Reason,
    double Risk,
    IReadOnlyList<string> Evidence);

public sealed record CompSuggestion(
    string TemplateId,
    string TemplateName,
    int Hits,
    IReadOnlyList<string> MissingUnits);