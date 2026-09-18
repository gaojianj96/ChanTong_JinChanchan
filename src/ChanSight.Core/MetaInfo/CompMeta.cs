namespace ChanSight.Core.MetaInfo;

public sealed record UnitBuild
{
    public string HeroName { get; init; } = string.Empty;

    public int Cost { get; init; }

    public IReadOnlyList<string> Items { get; init; } = Array.Empty<string>();

    public string? Note { get; init; }
}

public sealed record StageStep
{
    public string Stage { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;
}

public sealed record Variant
{
    public string Condition { get; init; } = string.Empty;

    public string Plan { get; init; } = string.Empty;
}

public sealed record Augment
{
    public string Name { get; init; } = string.Empty;

    public string? Stage { get; init; }

    public string? Effect { get; init; }
}

public sealed record TraitThreshold
{
    public string Trait { get; init; } = string.Empty;

    public IReadOnlyList<int> Thresholds { get; init; } = Array.Empty<int>();
}

public sealed record CompMeta
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string CompCode { get; init; } = string.Empty;

    public IntrinsicRequirements IntrinsicRequirements { get; init; } = null!;

    public SituationalThresholds? Situational { get; init; }

    public IReadOnlyList<UnitBuild> Units { get; init; } = Array.Empty<UnitBuild>();

    public IReadOnlyList<StageStep> Leveling { get; init; } = Array.Empty<StageStep>();

    public IReadOnlyList<Variant> Variants { get; init; } = Array.Empty<Variant>();

    public string? SettleNote { get; init; }

    public IReadOnlyList<Augment> Augments { get; init; } = Array.Empty<Augment>();

    public IReadOnlyList<TraitThreshold> Traits { get; init; } = Array.Empty<TraitThreshold>();

    public BoardLayout? BoardLayout { get; init; }

    public IReadOnlyList<string> Transformations { get; init; } = Array.Empty<string>();

    public MetaSource Source { get; init; } = null!;
}
