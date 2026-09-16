using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class RecognitionCostGuardTests
{
    [Fact]
    public void TryCharge_UnderBudget_AccumulatesAndReturnsTrue()
    {
        var guard = new RecognitionCostGuard(perGameBudgetUsd: 2.0, perCellUnitUsd: 0.001);

        var ok = guard.TryCharge(10, out var charged);

        ok.Should().BeTrue();
        charged.Should().BeApproximately(0.010, 1e-12);
        guard.SpentUsd.Should().BeApproximately(0.010, 1e-12);
    }

    [Fact]
    public void TryCharge_ExceedsBudget_RefusesWithoutAccumulating()
    {
        var guard = new RecognitionCostGuard(perGameBudgetUsd: 0.005, perCellUnitUsd: 0.001);

        var ok1 = guard.TryCharge(1, out _);
        var ok2 = guard.TryCharge(1, out _);
        var ok3 = guard.TryCharge(1, out _);
        var ok4 = guard.TryCharge(1, out _);
        var ok5 = guard.TryCharge(1, out _);
        var ok6 = guard.TryCharge(1, out var charged6);

        ok1.Should().BeTrue();
        ok2.Should().BeTrue();
        ok3.Should().BeTrue();
        ok4.Should().BeTrue();
        ok5.Should().BeTrue();
        ok6.Should().BeFalse();
        charged6.Should().BeApproximately(0.001, 1e-12);
        guard.SpentUsd.Should().BeApproximately(0.005, 1e-12);
    }

    [Fact]
    public void Reset_ReturnsSpendToZero()
    {
        var guard = new RecognitionCostGuard(perGameBudgetUsd: 1.0, perCellUnitUsd: 0.001);
        guard.TryCharge(5, out _);

        guard.Reset();

        guard.SpentUsd.Should().Be(0.0);

        var ok = guard.TryCharge(5, out _);
        ok.Should().BeTrue();
    }

    [Fact]
    public void DegradeToLocal_IsReadWriteAndDoesNotAffectCharging()
    {
        var guard = new RecognitionCostGuard(perGameBudgetUsd: 2.0, perCellUnitUsd: 0.001);

        guard.DegradeToLocal.Should().BeFalse();
        guard.DegradeToLocal = true;
        guard.DegradeToLocal.Should().BeTrue();

        var ok1 = guard.TryCharge(1, out _);
        var ok2 = guard.TryCharge(1, out _);

        ok1.Should().BeTrue();
        ok2.Should().BeTrue();
        guard.SpentUsd.Should().BeApproximately(0.002, 1e-12);
    }

    [Fact]
    public void TryCharge_Concurrent_DoesNotExceedBudget()
    {
        var guard = new RecognitionCostGuard(perGameBudgetUsd: 0.005, perCellUnitUsd: 0.001);
        const int workerCount = 32;
        const int attemptsPerWorker = 100;

        var threads = Enumerable.Range(0, workerCount)
            .Select(_ => new Thread(() =>
            {
                for (var i = 0; i < attemptsPerWorker; i++)
                {
                    guard.TryCharge(1, out double _);
                    if (guard.SpentUsd > 0.005 + 1e-12)
                        throw new InvalidOperationException("Budget exceeded.");
                }
            }))
            .ToArray();

        foreach (var t in threads)
            t.Start();
        foreach (var t in threads)
            t.Join();

        guard.SpentUsd.Should().BeLessThanOrEqualTo(0.005 + 1e-12);
    }
}