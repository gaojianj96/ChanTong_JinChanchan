namespace ChanSight.Core.MetaInfo;

public sealed record PlayerDeclaration
{
    public IReadOnlyDictionary<string, ConditionStatus> Conditions { get; init; } = new Dictionary<string, ConditionStatus>();
}
