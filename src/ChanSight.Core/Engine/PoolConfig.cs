namespace ChanSight.Core.Engine;

public sealed record PoolConfig
{
    public const int MinLevel = 1;
    public const int MaxLevel = 10;

    // Copy counts per champion, indexed by cost (1..5).
    private static readonly int[] DefaultCopiesPerChampion = { 0, 22, 20, 17, 10, 9 };

    // Number of distinct champions per cost (1..5).
    private static readonly int[] DefaultChampionCounts = { 0, 13, 13, 13, 12, 8 };

    // Level -> appearance probability per cost (1..5), each row summing to 100.
    private static readonly int[][] DefaultLevelOdds =
    {
        Array.Empty<int>(), // level 0 unused
        new[] { 100, 0, 0, 0, 0 },   // L1
        new[] { 100, 0, 0, 0, 0 },   // L2
        new[] { 75, 25, 0, 0, 0 },   // L3
        new[] { 55, 30, 15, 0, 0 },  // L4
        new[] { 45, 33, 20, 2, 0 },  // L5
        new[] { 30, 40, 25, 5, 0 },  // L6
        new[] { 19, 30, 35, 15, 1 }, // L7
        new[] { 18, 25, 32, 22, 3 }, // L8
        new[] { 10, 20, 25, 35, 10 },// L9
        new[] { 5, 10, 20, 40, 25 }, // L10
    };

    public PoolConfig()
        : this(DefaultCopiesPerChampion, DefaultChampionCounts, DefaultLevelOdds)
    {
    }

    public PoolConfig(IReadOnlyList<int> copiesPerChampion, IReadOnlyList<int> championCounts, IReadOnlyList<int[]> levelOdds)
    {
        if (copiesPerChampion.Count != 6)
        {
            throw new ArgumentException("CopiesPerChampion must contain exactly 6 entries (index 0 unused, costs 1..5).", nameof(copiesPerChampion));
        }

        if (championCounts.Count != 6)
        {
            throw new ArgumentException("ChampionCounts must contain exactly 6 entries (index 0 unused, costs 1..5).", nameof(championCounts));
        }

        if (levelOdds.Count != MaxLevel + 1)
        {
            throw new ArgumentException($"LevelOdds must contain {MaxLevel + 1} entries (levels 0..{MaxLevel}).", nameof(levelOdds));
        }

        for (int cost = 1; cost <= 5; cost++)
        {
            if (copiesPerChampion[cost] <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(copiesPerChampion), $"CopiesPerChampion[{cost}] must be positive.");
            }

            if (championCounts[cost] <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(championCounts), $"ChampionCounts[{cost}] must be positive.");
            }
        }

        for (int level = MinLevel; level <= MaxLevel; level++)
        {
            var odds = levelOdds[level];
            if (odds.Length != 5)
            {
                throw new ArgumentException($"LevelOdds[{level}] must contain 5 entries (costs 1..5).", nameof(levelOdds));
            }

            if (odds.Sum() != 100)
            {
                throw new ArgumentException($"LevelOdds[{level}] must sum to 100.", nameof(levelOdds));
            }

            if (odds.Any(o => o < 0))
            {
                throw new ArgumentException($"LevelOdds[{level}] must not contain negative values.", nameof(levelOdds));
            }
        }

        CopiesPerChampion = copiesPerChampion;
        ChampionCounts = championCounts;
        LevelOdds = levelOdds;
    }

    public IReadOnlyList<int> CopiesPerChampion { get; }

    public IReadOnlyList<int> ChampionCounts { get; }

    public IReadOnlyList<int[]> LevelOdds { get; }

    public int CopiesPerChampionFor(int cost) => cost is >= 1 and <= 5 ? CopiesPerChampion[cost] : 0;

    public int ChampionCountFor(int cost) => cost is >= 1 and <= 5 ? ChampionCounts[cost] : 0;

    public double ProbabilityAtLevel(int level, int cost)
    {
        if (level is < MinLevel or > MaxLevel)
        {
            return 0;
        }

        if (cost is < 1 or > 5)
        {
            return 0;
        }

        return LevelOdds[level][cost - 1] / 100.0;
    }

    public int TotalCopiesFor(int cost) => CopiesPerChampionFor(cost) * ChampionCountFor(cost);
}