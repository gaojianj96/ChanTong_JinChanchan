namespace ChanSight.Core.Engine;

public sealed record BoardUnitState(
    int SlotIndex,
    string? Name,
    int Star,
    int CopyCount,
    IReadOnlyList<string> Items);

public sealed record ShopCardState(
    int SlotIndex,
    string? Name,
    int Cost);

public sealed record OpponentSnapshot(
    int PlayerIndex,
    string? PlayerName,
    int? Hp,
    int? Level,
    int GoldEstimate,
    IReadOnlyList<BoardUnitState> BoardUnits,
    IReadOnlyList<BoardUnitState> BenchUnits,
    long Version);

public sealed record GameStateSnapshot(
    string Stage,
    GamePhase Phase,
    int Gold,
    int Level,
    int Exp,
    int Hp,
    int Streak,
    string? PlayerName,
    IReadOnlyList<BoardUnitState> BoardUnits,
    IReadOnlyList<BoardUnitState> BenchUnits,
    IReadOnlyList<ShopCardState> ShopCards,
    IReadOnlyList<OpponentSnapshot> Opponents,
    long Version);