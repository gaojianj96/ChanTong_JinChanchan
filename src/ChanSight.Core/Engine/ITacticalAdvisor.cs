namespace ChanSight.Core.Engine;

public interface ITacticalAdvisor
{
    IReadOnlyList<TacticalRecommendation> Evaluate(GameStateSnapshot state);
}