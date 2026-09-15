namespace ChanSight.Core.Engine;

public interface IHypergeometricEngine
{
    double ProbabilityAtLeast(int level, int targetCost, int owned, int takenByOthers, int needed);

    double ExpectedCopiesPerGold(int level, int targetCost, int owned, int takenByOthers, double gold);
}