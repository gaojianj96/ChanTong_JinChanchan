namespace ChanSight.Core.Data;

public sealed record CompTemplate(
    string Id,
    string Name,
    IReadOnlyList<string> KeyTraits,
    IReadOnlyList<string> CoreUnits,
    IReadOnlyList<string> SecondaryUnits,
    string Description);