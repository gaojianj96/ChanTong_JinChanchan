namespace ChanSight.Core.MetaInfo;

public sealed record CompRelation
{
    public string FromCompId { get; init; } = string.Empty;

    public string ToCompId { get; init; } = string.Empty;

    public RelationKind Kind { get; init; }

    public string? Condition { get; init; }

    public MetaSource Source { get; init; } = null!;
}

public sealed record Tip
{
    public string Id { get; init; } = string.Empty;

    public string? CompId { get; init; }

    public string Content { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

    public MetaSource Source { get; init; } = null!;
}
