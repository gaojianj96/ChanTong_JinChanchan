using ChanSight.Core.Engine;
using FluentAssertions;

namespace ChanSight.Tests.Engine;

public sealed class GameStateManagerTests
{
    [Fact]
    public void Current_InitialSnapshot_IsPlanningStageOneOneWithZeroGold()
    {
        var manager = new GameStateManager();

        var current = manager.Current;

        current.Phase.Should().Be(GamePhase.Planning);
        current.Stage.Should().Be("1-1");
        current.Gold.Should().Be(0);
        current.Level.Should().Be(1);
        current.Hp.Should().Be(100);
        current.Version.Should().Be(0);
        current.Opponents.Should().HaveCount(GameStateManager.OpponentCount);
        current.Opponents.Should().OnlyContain(o => o.Hp == null && o.Level == null);
    }

    [Fact]
    public void Current_InitialSnapshot_SchemaDefaultsArePopulated()
    {
        var manager = new GameStateManager();

        var current = manager.Current;

        current.PlayerName.Should().BeNull();
        current.BoardUnits.Should().BeEmpty();
        current.BenchUnits.Should().BeEmpty();

        current.Opponents.Should().OnlyContain(o =>
            o.PlayerName == null &&
            o.GoldEstimate == 0 &&
            o.BenchUnits.Count == 0);
    }

    [Fact]
    public void BoardUnitState_DefaultItems_IsEmpty()
    {
        var unit = new BoardUnitState(0, "Garen", 2, 1, Array.Empty<string>());

        unit.Items.Should().BeEmpty();
    }

    [Fact]
    public void OpponentSnapshot_Defaults_GoldEstimateZeroBenchEmptyPlayerNameNull()
    {
        var snap = new OpponentSnapshot(
            0,
            PlayerName: null,
            Hp: null,
            Level: null,
            GoldEstimate: 0,
            BoardUnits: Array.Empty<BoardUnitState>(),
            BenchUnits: Array.Empty<BoardUnitState>(),
            Version: 0);

        snap.PlayerName.Should().BeNull();
        snap.GoldEstimate.Should().Be(0);
        snap.BenchUnits.Should().BeEmpty();
    }

    [Fact]
    public void Transition_LegalChain_PlanningCombatCarouselCombat_Succeeds()
    {
        var manager = new GameStateManager();

        manager.Transition(GamePhase.Combat);
        manager.Current.Phase.Should().Be(GamePhase.Combat);

        manager.Transition(GamePhase.Carousel);
        manager.Current.Phase.Should().Be(GamePhase.Carousel);

        manager.Transition(GamePhase.Combat);
        manager.Current.Phase.Should().Be(GamePhase.Combat);

        manager.Current.Version.Should().Be(3);
    }

    [Theory]
    [InlineData(GamePhase.Planning)] // self-transition
    [InlineData(GamePhase.Carousel)] // Planning never leads directly to Carousel
    public void Transition_FromPlanning_IllegalTarget_Throws(GamePhase next)
    {
        var manager = new GameStateManager();

        var act = () => manager.Transition(next);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Transition_FromCombat_SelfTransition_Throws()
    {
        var manager = new GameStateManager();
        manager.Transition(GamePhase.Combat);

        var act = () => manager.Transition(GamePhase.Combat);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Update_AtomicallyReplacesSnapshot_AndIncrementsVersion()
    {
        var manager = new GameStateManager();
        var old = manager.Current;

        var next = old with { Stage = "2-1", Gold = 50, Version = 9999 };

        manager.Update(next);

        var current = manager.Current;

        current.Should().NotBeSameAs(old);
        current.Version.Should().Be(old.Version + 1);
        current.Stage.Should().Be("2-1");
        current.Gold.Should().Be(50);

        old.Version.Should().Be(0);
        old.Stage.Should().Be("1-1");
        old.Gold.Should().Be(0);
    }

    [Fact]
    public void Update_IgnoresIncomingVersion_AlwaysIncrementsCurrentVersion()
    {
        var manager = new GameStateManager();

        manager.Update(manager.Current with { Stage = "1-2", Version = 42 });
        manager.Update(manager.Current with { Stage = "1-3", Version = -7 });

        // Version is owned by the state machine: external values are discarded and
        // the merged snapshot's Version strictly increments by one each update.
        manager.Current.Version.Should().Be(2);
        manager.Current.Stage.Should().Be("1-3");
    }

    [Fact]
    public void UpdateOpponent_OnlyChangesTheTargetedSlot()
    {
        var manager = new GameStateManager();

        manager.Update(manager.Current with
        {
            Opponents = Enumerable.Range(0, GameStateManager.OpponentCount)
                .Select(i => new OpponentSnapshot(i, PlayerName: null, Hp: 100 - i, Level: i + 1, GoldEstimate: 0, Array.Empty<BoardUnitState>(), BenchUnits: Array.Empty<BoardUnitState>(), Version: i))
                .ToArray(),
        });

        var before = manager.Current;
        var replacement = new OpponentSnapshot(
            3,
            PlayerName: null,
            Hp: 7,
            Level: 2,
            GoldEstimate: 0,
            new[] { new BoardUnitState(0, "Yasuo", 1, 1, Array.Empty<string>()) },
            BenchUnits: Array.Empty<BoardUnitState>(),
            Version: 99);

        manager.UpdateOpponent(3, replacement);

        var after = manager.Current;

        after.Version.Should().Be(before.Version + 1);

        for (var i = 0; i < GameStateManager.OpponentCount; i++)
        {
            if (i == 3)
            {
                after.Opponents[i].Hp.Should().Be(7);
                after.Opponents[i].Level.Should().Be(2);
                after.Opponents[i].BoardUnits.Should().HaveCount(1);
            }
            else
            {
                after.Opponents[i].Hp.Should().Be(100 - i);
                after.Opponents[i].Level.Should().Be(i + 1);
                after.Opponents[i].BoardUnits.Should().BeEmpty();
            }
        }
    }

    [Fact]
    public void UpdateOpponent_OutOfRangeIndex_Throws()
    {
        var manager = new GameStateManager();
        var snap = new OpponentSnapshot(7, PlayerName: null, Hp: null, Level: null, GoldEstimate: 0, Array.Empty<BoardUnitState>(), BenchUnits: Array.Empty<BoardUnitState>(), Version: 0);

        var act = () => manager.UpdateOpponent(7, snap);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Current_ConcurrentReads_CompletesWithoutError()
    {
        var manager = new GameStateManager();

        Parallel.For(0, 10_000, i => { _ = manager.Current; });

        manager.Current.Should().NotBeNull();
    }

    [Fact]
    public void SetPhase_AppliesDirectlyAndIncrementsVersion()
    {
        var manager = new GameStateManager();

        manager.SetPhase(GamePhase.Combat);

        manager.Current.Phase.Should().Be(GamePhase.Combat);
        manager.Current.Version.Should().Be(1);
    }

    [Fact]
    public void SetPhase_BootstrapToCarousel_DoesNotThrow()
    {
        var manager = new GameStateManager();

        // Planning -> Carousel is illegal via Transition, but SetPhase bypasses
        // the transition table for mid-game bootstrap.
        manager.SetPhase(GamePhase.Carousel);

        manager.Current.Phase.Should().Be(GamePhase.Carousel);
    }
}