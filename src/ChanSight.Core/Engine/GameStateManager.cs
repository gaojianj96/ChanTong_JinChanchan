namespace ChanSight.Core.Engine;

public sealed class GameStateManager
{
    public const int OpponentCount = 7;

    private static readonly IReadOnlyDictionary<GamePhase, GamePhase[]> ValidTransitions =
        new Dictionary<GamePhase, GamePhase[]>
        {
            [GamePhase.Planning] = new[] { GamePhase.Combat, GamePhase.PvE },
            [GamePhase.Combat] = new[] { GamePhase.Planning, GamePhase.Carousel, GamePhase.PvE },
            [GamePhase.Carousel] = new[] { GamePhase.Planning, GamePhase.Combat },
            [GamePhase.PvE] = new[] { GamePhase.Planning, GamePhase.Carousel },
        };

    private volatile GameStateSnapshot _current;

    public GameStateManager()
    {
        _current = CreateInitialSnapshot();
    }

    public GameStateSnapshot Current => _current;

    public void Transition(GamePhase next)
    {
        var current = Current;
        if (!IsTransitionAllowed(current.Phase, next))
        {
            throw new InvalidOperationException($"Illegal phase transition: {current.Phase} -> {next}.");
        }

        _current = current with { Phase = next, Version = current.Version + 1 };
    }

    public void SetPhase(GamePhase phase)
    {
        var current = Current;
        _current = current with { Phase = phase, Version = current.Version + 1 };
    }

    public void Update(GameStateSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(next);

        _current = next with { Version = _current.Version + 1 };
    }

    public void UpdateOpponent(int playerIndex, OpponentSnapshot snap)
    {
        ArgumentNullException.ThrowIfNull(snap);

        if (playerIndex < 0 || playerIndex >= OpponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        }

        var current = Current;
        var opponents = current.Opponents.ToArray();
        opponents[playerIndex] = snap;

        _current = current with { Opponents = opponents, Version = current.Version + 1 };
    }

    private static bool IsTransitionAllowed(GamePhase from, GamePhase to) =>
        ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    private static GameStateSnapshot CreateInitialSnapshot() => new(
        Stage: "1-1",
        Phase: GamePhase.Planning,
        Gold: 0,
        Level: 1,
        Exp: 0,
        Hp: 100,
        Streak: 0,
        PlayerName: null,
        BoardUnits: Array.Empty<BoardUnitState>(),
        BenchUnits: Array.Empty<BoardUnitState>(),
        ShopCards: Array.Empty<ShopCardState>(),
        Opponents: Enumerable.Range(0, OpponentCount)
            .Select(i => new OpponentSnapshot(i, PlayerName: null, Hp: null, Level: null, GoldEstimate: 0, Array.Empty<BoardUnitState>(), BenchUnits: Array.Empty<BoardUnitState>(), Version: 0))
            .ToArray(),
        Version: 0);
}