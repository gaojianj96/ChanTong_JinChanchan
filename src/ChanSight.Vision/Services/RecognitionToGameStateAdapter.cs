using ChanSight.Core.Engine;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

public sealed class RecognitionToGameStateAdapter : IGameStateInputAdapter
{
    private readonly GameStateManager _manager;
    private readonly IPhaseDetector _phaseDetector;

    public RecognitionToGameStateAdapter(GameStateManager manager, IPhaseDetector? phaseDetector = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _phaseDetector = phaseDetector ?? new PhaseDetector();
    }

    public void Apply(RecognitionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var current = _manager.Current;
        var phase = _phaseDetector.Detect(frame);

        ApplyPhase(phase);

        var snapshot = new GameStateSnapshot(
            Stage: ParseStage(frame.Stage),
            Phase: phase,
            Gold: frame.Gold,
            Level: frame.Level,
            Exp: frame.Exp,
            Hp: frame.Hp,
            Streak: current.Streak,
            PlayerName: frame.PlayerName,
            BoardUnits: frame.BoardCells.Select((cell, index) => ToBoardUnit(index, cell)).ToArray(),
            BenchUnits: frame.BenchCells.Select((cell, index) => ToBoardUnit(index, cell)).ToArray(),
            ShopCards: frame.ShopCards.Select((card, index) => ToShopCard(index, card)).ToArray(),
            Opponents: current.Opponents,
            Version: current.Version);

        _manager.Update(snapshot);
    }

    /// <summary>
    /// Applies an opponent-perspective frame to the opponent snapshot at the given
    /// index (0-6). PlayerName/GoldEstimate/BoardUnits/BenchUnits are written to
    /// the corresponding <see cref="OpponentSnapshot"/>; the self snapshot is left
    /// untouched.
    /// </summary>
    public void ApplyOpponent(RecognitionFrame frame, int opponentIndex)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var current = _manager.Current;
        var existing = current.Opponents.ElementAtOrDefault(opponentIndex);

        var snapshot = new OpponentSnapshot(
            PlayerIndex: opponentIndex,
            PlayerName: frame.PlayerName,
            Hp: existing?.Hp,
            Level: existing?.Level,
            GoldEstimate: frame.GoldEstimate ?? existing?.GoldEstimate ?? 0,
            BoardUnits: frame.BoardCells.Select((cell, index) => ToBoardUnit(index, cell)).ToArray(),
            BenchUnits: frame.BenchCells.Select((cell, index) => ToBoardUnit(index, cell)).ToArray(),
            Version: existing?.Version ?? 0);

        _manager.UpdateOpponent(opponentIndex, snapshot);
    }

    private void ApplyPhase(GamePhase phase)
    {
        if (_manager.Current.Version == 0)
        {
            // Mid-game bootstrap: the first frame may legitimately be Combat,
            // Carousel or PvE, which Transition would reject.
            _manager.SetPhase(phase);
        }
        else if (phase != _manager.Current.Phase)
        {
            try
            {
                _manager.Transition(phase);
            }
            catch (InvalidOperationException)
            {
                // Illegal transition during normal flow (e.g. a transient
                // misclassification) is ignored rather than crashing.
            }
        }
    }

    private static BoardUnitState ToBoardUnit(int slotIndex, UnitCell cell)
    {
        var occupied = !string.IsNullOrWhiteSpace(cell.Name);
        return new BoardUnitState(
            SlotIndex: slotIndex,
            Name: occupied ? cell.Name : null,
            Star: occupied ? cell.Star : 0,
            CopyCount: occupied ? CopiesFor(cell.Star) : 0,
            Items: occupied && cell.Items is not null
                ? cell.Items.Select(i => i.IconId).ToArray()
                : Array.Empty<string>());
    }

    private static ShopCardState ToShopCard(int slotIndex, ShopCard card)
    {
        var occupied = !string.IsNullOrWhiteSpace(card.Name);
        return new ShopCardState(
            SlotIndex: slotIndex,
            Name: occupied ? card.Name : null,
            Cost: occupied ? card.Cost : 0);
    }

    // A unit embeds pool copies by star: 1★=1, 2★=3, 3★=9.
    private static int CopiesFor(int star) => star switch
    {
        1 => 1,
        2 => 3,
        >= 3 => 9,
        _ => 0,
    };

    private static string ParseStage(string stage) =>
        string.IsNullOrWhiteSpace(stage) ? "1-1" : stage.Trim();
}