using ChanSight.Core.Engine;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

public sealed class RecognitionToGameStateAdapter : IGameStateInputAdapter
{
    private readonly GameStateManager _manager;

    public RecognitionToGameStateAdapter(GameStateManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public void Apply(RecognitionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var current = _manager.Current;

        var snapshot = new GameStateSnapshot(
            Stage: ParseStage(frame.Stage),
            Phase: current.Phase,
            Gold: frame.Gold,
            Level: frame.Level,
            Exp: frame.Exp,
            Hp: frame.Hp,
            Streak: current.Streak,
            BoardUnits: frame.BoardCells.Select((cell, index) => ToBoardUnit(index, cell)).ToArray(),
            BenchUnits: frame.BenchCells.Select((cell, index) => ToBoardUnit(index, cell)).ToArray(),
            ShopCards: frame.ShopCards.Select((card, index) => ToShopCard(index, card)).ToArray(),
            Opponents: current.Opponents,
            Version: current.Version);

        _manager.Update(snapshot);
    }

    private static BoardUnitState ToBoardUnit(int slotIndex, UnitCell cell)
    {
        var occupied = !string.IsNullOrWhiteSpace(cell.Name);
        return new BoardUnitState(
            SlotIndex: slotIndex,
            Name: occupied ? cell.Name : null,
            Star: occupied ? cell.Star : 0,
            CopyCount: occupied ? CopiesFor(cell.Star) : 0);
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