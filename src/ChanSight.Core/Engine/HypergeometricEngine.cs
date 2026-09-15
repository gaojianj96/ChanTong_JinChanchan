namespace ChanSight.Core.Engine;

public sealed class HypergeometricEngine : IHypergeometricEngine
{
    private const int ShopSlots = 5;

    private readonly PoolConfig _config;

    public HypergeometricEngine(PoolConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public double ProbabilityAtLeast(int level, int targetCost, int owned, int takenByOthers, int needed)
    {
        if (needed <= 0)
        {
            return 1.0;
        }

        if (needed > ShopSlots)
        {
            return 0.0;
        }

        if (targetCost is < 1 or > 5)
        {
            return 0.0;
        }

        double costProbability = _config.ProbabilityAtLevel(level, targetCost);
        if (costProbability <= 0 || costProbability > 1)
        {
            return 0.0;
        }

        int copiesPerChampion = _config.CopiesPerChampionFor(targetCost);
        int championCount = _config.ChampionCountFor(targetCost);

        int totalCopies = copiesPerChampion * championCount;
        int remainingForTarget = copiesPerChampion - owned - takenByOthers;
        if (remainingForTarget <= 0)
        {
            return 0.0;
        }

        if (needed > remainingForTarget)
        {
            return 0.0;
        }

        int totalDrawPool = totalCopies - owned - takenByOthers;
        if (totalDrawPool < ShopSlots)
        {
            return 0.0;
        }

        double result = HypergeometricAtLeast(remainingForTarget, ShopSlots, needed, totalDrawPool);
        return result * costProbability;
    }

    public double ExpectedCopiesPerGold(int level, int targetCost, int owned, int takenByOthers, double gold)
    {
        if (gold <= 0)
        {
            return 0.0;
        }

        // Deterministic part: probability of seeing at least one copy in a single 5-slot refresh.
        double pOneOrMore = ProbabilityAtLeast(level, targetCost, owned, takenByOthers, 1);

        // Empirical approximation: expected copies in a refresh ≈ p(at least one) × 5, and
        // one refresh costs 2 gold. TODO(experience-based): refine with renewal/sampling data.
        double expectedCopiesPerRefresh = pOneOrMore * ShopSlots;
        double refreshes = gold / 2.0;
        return expectedCopiesPerRefresh * refreshes;
    }

    private static double HypergeometricAtLeast(int success, int draws, int needed, int population)
    {
        // P(X >= needed) where X ~ Hypergeometric(population successes, population total, draws).
        // Sum the probability mass exactly via log-binomial coefficients to avoid overflow/underflow.
        double sum = 0.0;
        for (int k = needed; k <= Math.Min(draws, success); k++)
        {
            sum += HypergeometricPmf(success, draws, k, population);
        }

        return Clamp(sum);
    }

    private static double HypergeometricPmf(int success, int draws, int k, int population)
    {
        // log C(success, k) + log C(population - success, draws - k) - log C(population, draws)
        double logP = LogBinomial(success, k)
            + LogBinomial(population - success, draws - k)
            - LogBinomial(population, draws);
        return Math.Exp(logP);
    }

    private static double LogBinomial(int n, int k)
    {
        if (k < 0 || k > n)
        {
            return double.NegativeInfinity;
        }

        if (k == 0 || k == n)
        {
            return 0.0;
        }

        // Symmetry: C(n, k) == C(n, n - k), use smaller k to reduce iteration count.
        if (k > n - k)
        {
            k = n - k;
        }

        double result = 0.0;
        for (int i = 1; i <= k; i++)
        {
            result += Math.Log(n - k + i) - Math.Log(i);
        }

        return result;
    }

    private static double Clamp(double value)
    {
        if (double.IsNaN(value))
        {
            return 0.0;
        }

        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}