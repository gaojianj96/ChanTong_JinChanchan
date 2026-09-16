using ChanSight.Core.Engine;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Engine;

public sealed class RecognitionToGameStateAdapterTests
{
    [Fact]
    public void Apply_MapsRecognitionFieldsToSnapshot()
    {
        var manager = new GameStateManager();
        var adapter = new RecognitionToGameStateAdapter(manager);

        var frame = new RecognitionFrame
        {
            Stage = " 3-2 ",
            Gold = 47,
            Level = 6,
            Exp = 12,
            Hp = 88,
            BoardCells = CreateBoard(28),
            BenchCells = CreateBench(9),
            ShopCards = CreateShop(5),
        };

        adapter.Apply(frame);

        var snap = manager.Current;

        snap.Stage.Should().Be("3-2");
        snap.Gold.Should().Be(47);
        snap.Level.Should().Be(6);
        snap.Exp.Should().Be(12);
        snap.Hp.Should().Be(88);
        snap.Phase.Should().Be(GamePhase.Planning);
        snap.BoardUnits.Should().HaveCount(28);
        snap.BenchUnits.Should().HaveCount(9);
        snap.ShopCards.Should().HaveCount(5);
        snap.Version.Should().Be(2);
    }

    [Fact]
    public void Apply_MapsBoardBenchShopGridsWithStarsAndCopyCounts()
    {
        var manager = new GameStateManager();
        var adapter = new RecognitionToGameStateAdapter(manager);

        var frame = new RecognitionFrame
        {
            Stage = "4-1",
            BoardCells = CreateBoard(28),
            BenchCells = CreateBench(9),
            ShopCards = CreateShop(5),
        };

        adapter.Apply(frame);

        var snap = manager.Current;

        var boardYasuo = snap.BoardUnits[0];
        boardYasuo.SlotIndex.Should().Be(0);
        boardYasuo.Name.Should().Be("Yasuo");
        boardYasuo.Star.Should().Be(2);
        boardYasuo.CopyCount.Should().Be(3);

        var boardGaren = snap.BoardUnits[1];
        boardGaren.Name.Should().Be("Garen");
        boardGaren.Star.Should().Be(1);
        boardGaren.CopyCount.Should().Be(1);

        snap.BoardUnits[2].Name.Should().BeNull();
        snap.BoardUnits[2].Star.Should().Be(0);
        snap.BoardUnits[2].CopyCount.Should().Be(0);

        var benchAatrox = snap.BenchUnits[0];
        benchAatrox.SlotIndex.Should().Be(0);
        benchAatrox.Name.Should().Be("Aatrox");
        benchAatrox.Star.Should().Be(3);
        benchAatrox.CopyCount.Should().Be(9);

        snap.BenchUnits[1].Name.Should().BeNull();

        var shopSoraka = snap.ShopCards[0];
        shopSoraka.SlotIndex.Should().Be(0);
        shopSoraka.Name.Should().Be("Soraka");
        shopSoraka.Cost.Should().Be(4);

        snap.ShopCards[1].Name.Should().BeNull();
        snap.ShopCards[1].Cost.Should().Be(0);
    }

    [Fact]
    public void Apply_FirstFrame_EmptyShopCombatStage_DetectsCombatNotPlanning()
    {
        var manager = new GameStateManager();
        var adapter = new RecognitionToGameStateAdapter(manager);

        var frame = new RecognitionFrame
        {
            Stage = "3-2",
            BoardCells = CreateBoard(28),
            BenchCells = CreateBench(9),
            ShopCards = Array.Empty<ShopCard>(),
        };

        adapter.Apply(frame);

        manager.Current.Phase.Should().Be(GamePhase.Combat);
    }

    [Fact]
    public void Apply_SubsequentFrames_PhaseUpdatesContinuously()
    {
        var manager = new GameStateManager();
        var adapter = new RecognitionToGameStateAdapter(manager);

        // First frame: empty shop + "3-2" -> Combat (bootstrap).
        adapter.Apply(new RecognitionFrame
        {
            Stage = "3-2",
            BoardCells = CreateBoard(28),
            BenchCells = CreateBench(9),
            ShopCards = Array.Empty<ShopCard>(),
        });
        manager.Current.Phase.Should().Be(GamePhase.Combat);

        // Next frame: shop visible -> Planning (legal transition from Combat).
        adapter.Apply(new RecognitionFrame
        {
            Stage = "3-2",
            BoardCells = CreateBoard(28),
            BenchCells = CreateBench(9),
            ShopCards = CreateShop(5),
        });
        manager.Current.Phase.Should().Be(GamePhase.Planning);

        // Next frame: empty shop + PvE round -> PvE (legal transition from Planning).
        adapter.Apply(new RecognitionFrame
        {
            Stage = "3-6",
            BoardCells = CreateBoard(28),
            BenchCells = CreateBench(9),
            ShopCards = Array.Empty<ShopCard>(),
        });
        manager.Current.Phase.Should().Be(GamePhase.PvE);
    }

    private static IReadOnlyList<UnitCell> CreateBoard(int size)
    {
        var cells = Enumerable.Range(0, size)
            .Select(_ => EmptyCell())
            .ToArray();
        cells[0] = new UnitCell("Yasuo", 2, Array.Empty<ItemStack>(), 0.9, SourceTier.T0);
        cells[1] = new UnitCell("Garen", 1, Array.Empty<ItemStack>(), 0.8, SourceTier.T0);
        return cells;
    }

    private static IReadOnlyList<UnitCell> CreateBench(int size)
    {
        var cells = Enumerable.Range(0, size)
            .Select(_ => EmptyCell())
            .ToArray();
        cells[0] = new UnitCell("Aatrox", 3, Array.Empty<ItemStack>(), 0.95, SourceTier.T0);
        return cells;
    }

    private static IReadOnlyList<ShopCard> CreateShop(int size)
    {
        var cards = Enumerable.Range(0, size)
            .Select(_ => new ShopCard(string.Empty, 0, 0.0, SourceTier.T3))
            .ToArray();
        cards[0] = new ShopCard("Soraka", 4, 0.9, SourceTier.T1);
        return cards;
    }

    private static UnitCell EmptyCell() =>
        new(null, 0, Array.Empty<ItemStack>(), 0.0, SourceTier.T3);
}