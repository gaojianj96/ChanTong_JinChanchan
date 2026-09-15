using ChanSight.Core.Engine;
using FluentAssertions;

namespace ChanSight.Tests.Engine;

public class HypergeometricEngineTests
{
    // Simplified pool: only 1-cost champions are ever offered (level odds are 100% cost-1 at
    // every level), with the standard 13 distinct 1-cost champions of 22 copies each. With
    // nobody holding any copies the cost-1 pool has N = 22 × 13 = 286 cards, of which K = 22
    // are the target, so P(≥1) reduces to the hypergeometric draw of 5 from 286.
    private static PoolConfig CreateCostOneOnlyPool() => new(
        copiesPerChampion: new[] { 0, 22, 20, 17, 10, 9 },
        championCounts: new[] { 0, 13, 13, 13, 12, 8 },
        levelOdds: new[]
        {
            Array.Empty<int>(),
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
            new[] { 100, 0, 0, 0, 0 },
        });

    private static double LogBinomial(int n, int k)
    {
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

    [Fact]
    public void ProbabilityAtLeast_SimplifiedPool_MatchesAnalyticFormula()
    {
        // P(≥1) = 1 - C(N-K, 5)/C(N, 5) with N = 22 × 13 = 286 and K = 22 target copies.
        const int copiesPerChampion = 22;
        const int championCount = 13;
        const int slots = 5;
        const int n = copiesPerChampion * championCount;
        const int k = copiesPerChampion;
        double expected = 1.0 - Math.Exp(LogBinomial(n - k, slots) - LogBinomial(n, slots));

        var engine = new HypergeometricEngine(CreateCostOneOnlyPool());
        double actual = engine.ProbabilityAtLeast(level: 1, targetCost: 1, owned: 0, takenByOthers: 0, needed: 1);

        actual.Should().BeApproximately(expected, 1e-12);
        actual.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void ProbabilityAtLeast_ZeroCopiesLeft_ReturnsZero()
    {
        var engine = new HypergeometricEngine(new PoolConfig());

        // 22 total copies, all held -> remaining 0.
        double result = engine.ProbabilityAtLeast(1, 1, owned: 22, takenByOthers: 0, needed: 1);
        result.Should().Be(0.0);
    }

    [Fact]
    public void ProbabilityAtLeast_AllCopiesHeldBetweenSelfAndOthers_ReturnsZero()
    {
        var engine = new HypergeometricEngine(new PoolConfig());

        // owned + takenByOthers exhausts the 22 copies of a 1-cost champion.
        double result = engine.ProbabilityAtLeast(1, 1, owned: 10, takenByOthers: 12, needed: 1);
        result.Should().Be(0.0);
    }

    [Fact]
    public void ProbabilityAtLeast_NeededExceedsRemaining_ReturnsZero()
    {
        var engine = new HypergeometricEngine(new PoolConfig());
        double result = engine.ProbabilityAtLeast(1, 1, owned: 20, takenByOthers: 0, needed: 3);
        result.Should().Be(0.0);
    }

    [Fact]
    public void ProbabilityAtLeast_MoreCopiesRemaining_YieldsHigherProbability()
    {
        var engine = new HypergeometricEngine(new PoolConfig());

        double fewerOwned = engine.ProbabilityAtLeast(6, 3, owned: 5, takenByOthers: 0, needed: 1);
        double moreOwned = engine.ProbabilityAtLeast(6, 3, owned: 10, takenByOthers: 0, needed: 1);

        fewerOwned.Should().BeGreaterThan(moreOwned);
    }

    [Fact]
    public void ProbabilityAtLeast_HigherLevel_YieldsHigherProbabilityForHighCost()
    {
        var engine = new HypergeometricEngine(new PoolConfig());

        double lowLevel = engine.ProbabilityAtLeast(5, 5, owned: 0, takenByOthers: 0, needed: 1);
        double highLevel = engine.ProbabilityAtLeast(9, 5, owned: 0, takenByOthers: 0, needed: 1);

        highLevel.Should().BeGreaterThan(lowLevel);
    }

    [Fact]
    public void ProbabilityAtLeast_NeededBeyondShopSlots_ReturnsZero()
    {
        var engine = new HypergeometricEngine(new PoolConfig());
        double result = engine.ProbabilityAtLeast(6, 1, owned: 0, takenByOthers: 0, needed: 6);
        result.Should().Be(0.0);
    }

    [Fact]
    public void ProbabilityAtLeast_NeededZero_ReturnsDeterministicOne()
    {
        var engine = new HypergeometricEngine(new PoolConfig());
        double result = engine.ProbabilityAtLeast(6, 1, owned: 0, takenByOthers: 0, needed: 0);
        result.Should().Be(1.0);
    }

    [Fact]
    public void ProbabilityAtLeast_AcrossCosts_AlwaysWithinUnitIntervalAndMonotonicByLevel()
    {
        var engine = new HypergeometricEngine(new PoolConfig());

        for (int cost = 1; cost <= 5; cost++)
        {
            double previous = -1.0;
            for (int level = 1; level <= 10; level++)
            {
                double value = engine.ProbabilityAtLeast(level, cost, owned: 0, takenByOthers: 0, needed: 1);

                value.Should().BeInRange(0.0, 1.0);
                double.IsNaN(value).Should().BeFalse();

                // Only 4- and 5-cost champions appear more at higher levels in the default
                // odds table; 1-cost strictly decreases and mid costs are unimodal per level.
                if (cost >= 4 && previous >= 0)
                {
                    value.Should().BeGreaterThanOrEqualTo(previous);
                }

                previous = value;
            }
        }
    }

    [Fact]
    public void ExpectedCopiesPerGold_ZeroGold_ReturnsZero()
    {
        var engine = new HypergeometricEngine(new PoolConfig());
        engine.ExpectedCopiesPerGold(6, 1, 0, 0, 0).Should().Be(0.0);
    }

    [Fact]
    public void ExpectedCopiesPerGold_PositiveGold_ReturnsNonNegative()
    {
        var engine = new HypergeometricEngine(new PoolConfig());
        double value = engine.ExpectedCopiesPerGold(6, 1, 0, 0, gold: 10.0);
        value.Should().BeGreaterThanOrEqualTo(0.0);
        double.IsNaN(value).Should().BeFalse();
    }
}