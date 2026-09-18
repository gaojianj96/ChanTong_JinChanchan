namespace ChanSight.Core.MetaInfo;

public sealed record MetaSource
{
    public MetaSourceType Type { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string Patch { get; init; } = string.Empty;

    public string? Url { get; init; }

    public double Confidence { get; init; }
}
