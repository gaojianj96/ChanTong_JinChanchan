using System.Threading;

namespace ChanSight.Vision.Services;

/// <summary>
/// Enforces a per-game cost budget for the recognition chain. Once the accumulated
/// charge would exceed the budget, further charges are refused until reset. A
/// separate degrade switch signals callers to skip VLM and fall back to local CV
/// rules. Thread-safe via <see cref="Interlocked"/>.
/// </summary>
public sealed class RecognitionCostGuard
{
    private const double UnitsPerUsd = 1000.0;
    private const double Tolerance = 1e-12;

    private readonly double _perGameBudgetUsd;
    private readonly double _perCellUnitUsd;
    private double _spentUsd;
    private int _degradeToLocal;

    public RecognitionCostGuard(double perGameBudgetUsd = 2.0, double perCellUnitUsd = 0.001)
    {
        _perGameBudgetUsd = perGameBudgetUsd;
        _perCellUnitUsd = perCellUnitUsd;
    }

    /// <summary>Total budget charged so far, in USD.</summary>
    public double SpentUsd => Volatile.Read(ref _spentUsd);

    /// <summary>
    /// When true, callers should skip VLM and use local CV rules only. This guard
    /// records the state but does not itself enforce skipping.
    /// </summary>
    public bool DegradeToLocal
    {
        get => Volatile.Read(ref _degradeToLocal) != 0;
        set => Volatile.Write(ref _degradeToLocal, value ? 1 : 0);
    }

    /// <summary>
    /// Attempts to charge <paramref name="cellCount"/> cells. Returns false (and
    /// charges nothing) when the charge would push cumulative spend over the budget.
    /// </summary>
    public bool TryCharge(int cellCount, out double chargedUsd)
    {
        chargedUsd = cellCount * _perCellUnitUsd;

        while (true)
        {
            var current = Volatile.Read(ref _spentUsd);
            var next = current + chargedUsd;

            if (next > _perGameBudgetUsd + Tolerance)
                return false;

            var original = Interlocked.CompareExchange(ref _spentUsd, next, current);
            if (original == current)
                return true;
        }
    }

    /// <summary>Resets the accumulated spend to zero.</summary>
    public void Reset() => Interlocked.Exchange(ref _spentUsd, 0.0);
}