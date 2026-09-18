namespace ChanSight.Core.MetaInfo;

public sealed record ConditionalTierShift
{
    public string Condition { get; init; } = string.Empty;

    public CompTier? ShiftedTier { get; init; }

    public string? Effect { get; init; }
}

public sealed record TierEntry
{
    public string CompId { get; init; } = string.Empty;

    public CompTier Tier { get; init; }

    public IReadOnlyList<ConditionalTierShift> Conditionals { get; init; } = Array.Empty<ConditionalTierShift>();

    public string? Note { get; init; }

    public MetaSource Source { get; init; } = null!;
}

public sealed record VersionMeta
{
    public string Patch { get; init; } = string.Empty;

    public string? EnvironmentNote { get; init; }

    public string? Playstyle { get; init; }

    public IReadOnlyList<TierEntry> Tiers { get; init; } = Array.Empty<TierEntry>();

    public MetaSource Source { get; init; } = null!;
}
