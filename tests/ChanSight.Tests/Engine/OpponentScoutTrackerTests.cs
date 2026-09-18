using ChanSight.Core.Engine;
using FluentAssertions;

namespace ChanSight.Tests.Engine;

public sealed class OpponentScoutTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ObserveBoard_FirstSwitchToOpponent0_WritesSlot0AndIncrementsVersion()
    {
        var manager = new GameStateManager();
        var tracker = new OpponentScoutTracker(manager);

        var board = new[] { new BoardUnitState(0, "Yasuo", 2, 1, Array.Empty<string>()) };

        tracker.ObserveBoard(0, board, hp: 80, level: 5, stage: "2-1", capturedAt: Now);

        manager.Current.Version.Should().Be(1);

        var opponent = manager.Current.Opponents[0];
        opponent.Hp.Should().Be(80);
        opponent.Level.Should().Be(5);
        opponent.BoardUnits.Should().HaveCount(1);
        opponent.BoardUnits[0].Name.Should().Be("Yasuo");

        tracker.CurrentFocus.Should().Be(0);
        tracker.LastSeenVersion[0].Should().Be(1);
    }

    [Fact]
    public void ObserveBoard_SameOpponentTwiceUnchanged_SecondObservationIsDeduped()
    {
        var manager = new GameStateManager();
        var tracker = new OpponentScoutTracker(manager);

        var board = new[] { new BoardUnitState(0, "Yasuo", 2, 1, Array.Empty<string>()) };

        tracker.ObserveBoard(0, board, hp: 80, level: 5, stage: "2-1", capturedAt: Now);
        tracker.ObserveBoard(0, board, hp: 80, level: 5, stage: "2-1", capturedAt: Now);

        manager.Current.Version.Should().Be(1);
        tracker.LastSeenVersion[0].Should().Be(1);
    }

    [Fact]
    public void ObserveBoard_BoardChanged_UpdatesAgainAndIncrementsVersion()
    {
        var manager = new GameStateManager();
        var tracker = new OpponentScoutTracker(manager);

        var before = new[] { new BoardUnitState(0, "Yasuo", 2, 1, Array.Empty<string>()) };
        var after = new[] { new BoardUnitState(0, "Yasuo", 3, 1, Array.Empty<string>()) };

        tracker.ObserveBoard(0, before, hp: 80, level: 5, stage: "2-1", capturedAt: Now);
        tracker.ObserveBoard(0, after, hp: 80, level: 5, stage: "2-1", capturedAt: Now);

        manager.Current.Version.Should().Be(2);
        manager.Current.Opponents[0].BoardUnits[0].Star.Should().Be(3);
        tracker.LastSeenVersion[0].Should().Be(2);
    }

    [Fact]
    public void ObserveBoard_StageChangedWithSameBoard_StillUpdates()
    {
        var manager = new GameStateManager();
        var tracker = new OpponentScoutTracker(manager);

        var board = new[] { new BoardUnitState(0, "Yasuo", 2, 1, Array.Empty<string>()) };

        tracker.ObserveBoard(0, board, hp: 80, level: 5, stage: "2-1", capturedAt: Now);
        tracker.ObserveBoard(0, board, hp: 80, level: 5, stage: "3-2", capturedAt: Now);

        manager.Current.Version.Should().Be(2);
        tracker.LastSeenVersion[0].Should().Be(2);
    }

    [Fact]
    public void ObserveBoard_OwnBoard_DoesNotWriteAnyOpponentAndUpdatesFocus()
    {
        var manager = new GameStateManager();
        var tracker = new OpponentScoutTracker(manager);

        var ownBoard = new[] { new BoardUnitState(0, "Katarina", 2, 1, Array.Empty<string>()) };

        tracker.ObserveBoard(-1, ownBoard, hp: 100, level: 6, stage: "2-1", capturedAt: Now);

        tracker.CurrentFocus.Should().Be(-1);
        manager.Current.Version.Should().Be(0);
        manager.Current.Opponents.Should().OnlyContain(
            o => o.Hp == null && o.Level == null && o.BoardUnits.Count == 0);
    }

    [Fact]
    public void ObserveBoard_ScoutSequence_WritesOnlyTargetedSlotsAndTracksFocus()
    {
        var manager = new GameStateManager();
        var tracker = new OpponentScoutTracker(manager);

        var board1 = new[] { new BoardUnitState(0, "Yasuo", 2, 1, Array.Empty<string>()) };
        var board3 = new[] { new BoardUnitState(0, "Garen", 1, 1, Array.Empty<string>()) };

        tracker.ObserveBoard(-1, Array.Empty<BoardUnitState>(), hp: null, level: null, stage: "2-1", capturedAt: Now);
        tracker.ObserveBoard(1, board1, hp: 80, level: 5, stage: "2-1", capturedAt: Now);
        tracker.ObserveBoard(-1, Array.Empty<BoardUnitState>(), hp: null, level: null, stage: "2-1", capturedAt: Now);
        tracker.ObserveBoard(1, board1, hp: 80, level: 5, stage: "2-1", capturedAt: Now);
        tracker.ObserveBoard(3, board3, hp: 70, level: 4, stage: "2-1", capturedAt: Now);

        var opponents = manager.Current.Opponents;

        opponents[1].BoardUnits.Should().HaveCount(1);
        opponents[1].BoardUnits[0].Name.Should().Be("Yasuo");
        opponents[1].Hp.Should().Be(80);
        opponents[1].Level.Should().Be(5);

        opponents[3].BoardUnits.Should().HaveCount(1);
        opponents[3].BoardUnits[0].Name.Should().Be("Garen");
        opponents[3].Hp.Should().Be(70);
        opponents[3].Level.Should().Be(4);

        foreach (var index in new[] { 0, 2, 4, 5, 6 })
        {
            opponents[index].Hp.Should().BeNull();
            opponents[index].Level.Should().BeNull();
            opponents[index].BoardUnits.Should().BeEmpty();
        }

        tracker.CurrentFocus.Should().Be(3);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(7)]
    public void ObserveBoard_InvalidPlayerIndex_ThrowsArgumentOutOfRange(int playerIndex)
    {
        var tracker = new OpponentScoutTracker(new GameStateManager());

        var act = () => tracker.ObserveBoard(
            playerIndex,
            Array.Empty<BoardUnitState>(),
            hp: null,
            level: null,
            stage: "2-1",
            capturedAt: Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ObserveBoard_NullBoard_ThrowsArgumentNull()
    {
        var tracker = new OpponentScoutTracker(new GameStateManager());

        var act = () => tracker.ObserveBoard(
            playerIndex: 0,
            board: null!,
            hp: null,
            level: null,
            stage: "2-1",
            capturedAt: Now);

        act.Should().Throw<ArgumentNullException>();
    }
}