namespace ChanSight.Core.MetaInfo;

public sealed record UnitCountRequirement
{
    public string UnitName { get; init; } = string.Empty;

    public int MinCount { get; init; }
}

public sealed record IntrinsicRequirements
{
    public IReadOnlyList<UnitCountRequirement> RequiredTwoStarUnits { get; init; } = Array.Empty<UnitCountRequirement>();

    public IReadOnlyList<string> RequiredEmblems { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredItems { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredAugments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<UnitCountRequirement> MinUnitCounts { get; init; } = Array.Empty<UnitCountRequirement>();
}

public sealed record SituationalThresholds
{
    public int? MinGold { get; init; }

    public int? MinHp { get; init; }

    public int? MinLevel { get; init; }

    public string? ByStage { get; init; }

    public int? MaxContestedPlayers { get; init; }

    public string? BoardConflictNote { get; init; }
}
