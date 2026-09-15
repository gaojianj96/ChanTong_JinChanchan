using ChanSight.Core.Engine;

namespace ChanSight.Vision.Models;

public sealed record DecisionPanelResult(
    GameStateSnapshot State,
    IReadOnlyList<TacticalRecommendation> AlgorithmAdvice,
    AdvisorSuggestion? AdvisorAdvice);