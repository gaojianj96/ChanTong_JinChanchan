namespace ChanSight.Core.Engine;

public sealed class OpponentScoutTracker
{
    private readonly GameStateManager _manager;
    private readonly ScoutMemo?[] _lastSeen = new ScoutMemo?[GameStateManager.OpponentCount];
    private readonly long[] _lastSeenVersion = new long[GameStateManager.OpponentCount];

    private int _currentFocus = -1;

    public OpponentScoutTracker(GameStateManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public int CurrentFocus => _currentFocus;

    public IReadOnlyList<long> LastSeenVersion => _lastSeenVersion;

    public void ObserveBoard(
        int playerIndex,
        IReadOnlyList<BoardUnitState> board,
        int? hp,
        int? level,
        string stage,
        DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (playerIndex < -1 || playerIndex >= GameStateManager.OpponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        }

        _currentFocus = playerIndex;

        // Own-board observations only advance the focus; they never pollute opponent slots.
        if (playerIndex < 0)
        {
            return;
        }

        var boardPending = IsBoardPending(board);
        var fingerprint = boardPending ? null : MakeFingerprint(board);

        var memo = _lastSeen[playerIndex];
        if (!boardPending &&
            memo is not null &&
            memo.Stage == stage &&
            memo.Hp == hp &&
            memo.Level == level &&
            memo.BoardFingerprint == fingerprint)
        {
            return;
        }

        var snap = new OpponentSnapshot(
            playerIndex,
            PlayerName: null,
            Hp: hp,
            Level: level,
            GoldEstimate: 0,
            BoardUnits: board,
            BenchUnits: Array.Empty<BoardUnitState>(),
            Version: _manager.Current.Version + 1);

        _manager.UpdateOpponent(playerIndex, snap);

        _lastSeen[playerIndex] = new ScoutMemo(stage, fingerprint, hp, level);
        _lastSeenVersion[playerIndex] = _manager.Current.Version;
    }

    private static bool IsBoardPending(IReadOnlyList<BoardUnitState> board) =>
        board.Count == 0 || board.All(unit => unit.Name is null);

    private static string MakeFingerprint(IReadOnlyList<BoardUnitState> board) =>
        string.Join(';', board.Select(unit => $"{unit.SlotIndex}:{unit.Name ?? "~"}:{unit.Star}"));

    private sealed record ScoutMemo(
        string Stage,
        string? BoardFingerprint,
        int? Hp,
        int? Level);
}