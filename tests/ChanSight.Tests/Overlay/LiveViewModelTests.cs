using System.Text.Json;
using ChanSight.Core.Annotation;
using ChanSight.Core.Engine;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.ViewModels;
using ChanSight.Vision.Models;
using FluentAssertions;

namespace ChanSight.Tests.Overlay;

public sealed class LiveViewModelTests
{
    [Fact]
    public void ApplyUpdate_UpdatesScalarFieldsAndGrids()
    {
        var vm = CreateViewModel(out _);
        var update = CreateUpdate(gold: 52, level: 7, stage: "3-2", hp: 63);

        vm.ApplyUpdate(update);

        vm.Gold.Should().Be(52);
        vm.Level.Should().Be(7);
        vm.Stage.Should().Be("3-2");
        vm.Hp.Should().Be(63);
        vm.BoardCells.Should().HaveCount(28);
        vm.BenchCells.Should().HaveCount(9);
        vm.ShopCards.Should().HaveCount(5);
    }

    [Fact]
    public async Task PinAndCommit_AnchorsCorrectionToPinnedFrameId()
    {
        var vm = CreateViewModel(out var fs);
        vm.ApplyUpdate(CreateUpdate(frameId: "pin-frame-123"));

        vm.PinNow();
        vm.PinnedFrameId.Should().Be("pin-frame-123");

        await vm.CommitHeroCorrectionAsync(RegionTypes.BoardHero, 0, "厄斐琉斯");

        var record = DeserializeLastCorrection(fs);
        record.FrameId.Should().Be("pin-frame-123");
        record.RegionType.Should().Be(RegionTypes.BoardHero);
        record.CellIndex.Should().Be(0);
    }

    [Fact]
    public void PinFreezesDisplay_NoFurtherUpdatesApplied()
    {
        var vm = CreateViewModel(out _);
        vm.ApplyUpdate(CreateUpdate(gold: 10, frameId: "f1"));
        vm.PinNow();

        vm.ApplyUpdate(CreateUpdate(gold: 99, frameId: "f2"));

        vm.IsPinned.Should().BeTrue();
        vm.Gold.Should().Be(10);
    }

    [Fact]
    public async Task CommitHeroCorrection_WithoutPin_DoesNotAppend()
    {
        var vm = CreateViewModel(out var fs);
        vm.ApplyUpdate(CreateUpdate(frameId: "f1"));

        await vm.CommitHeroCorrectionAsync(RegionTypes.BoardHero, 0, "厄斐琉斯");

        fs.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task StickyCorrection_DisplaysCorrectedValue()
    {
        var vm = CreateViewModel(out _);
        vm.ApplyUpdate(CreateUpdate(frameId: "f1"));
        vm.PinNow();

        await vm.CommitHeroCorrectionAsync(RegionTypes.BoardHero, 0, "厄斐琉斯");

        var cell = vm.BoardCells[0];
        cell.HasCorrection.Should().BeTrue();
        cell.Name.Should().Be("厄斐琉斯");
    }

    [Fact]
    public void Recalculate_PopulatesRecommendationsFromEvaluator()
    {
        var vm = CreateViewModel(out _);
        vm.ApplyUpdate(CreateUpdate(gold: 60, level: 5));

        vm.RecalculateRecommendations();

        vm.Recommendations.Should().NotBeEmpty();
    }

    private static LiveViewModel CreateViewModel(out FakeFileSystem fs)
    {
        fs = new FakeFileSystem();
        var store = new AnnotationStore(fs, "corrections");

        // 重算闭包: 直接返回一条固定算法建议, 不触及真实 TacticalAdvisor。
        Func<GameStateSnapshot, DecisionPanelResult> evaluate = state => new DecisionPanelResult(
            state,
            new[]
            {
                new TacticalRecommendation(AdviceKind.EconomyDecision, Verdict.Hold, 60, "保持持息", 0.1, Array.Empty<string>()),
            },
            AdvisorAdvice: null);

        return new LiveViewModel(store, evaluate);
    }

    private static LiveFrameUpdate CreateUpdate(
        int gold = 0,
        int level = 1,
        string stage = "1-1",
        int hp = 100,
        string? frameId = "frame-1")
    {
        var state = new GameStateSnapshot(
            Stage: stage,
            Phase: GamePhase.Planning,
            Gold: gold,
            Level: level,
            Exp: 0,
            Hp: hp,
            Streak: 0,
            PlayerName: null,
            BoardUnits: Enumerable.Range(0, 28).Select(i => new BoardUnitState(i, i == 0 ? "盖伦" : null, i == 0 ? 1 : 0, i == 0 ? 1 : 0, Array.Empty<string>())).ToArray(),
            BenchUnits: Enumerable.Range(0, 9).Select(i => new BoardUnitState(i, null, 0, 0, Array.Empty<string>())).ToArray(),
            ShopCards: Enumerable.Range(0, 5).Select(i => new ShopCardState(i, null, 0)).ToArray(),
            Opponents: Array.Empty<OpponentSnapshot>(),
            Version: 1);

        var frame = new RecognitionFrame
        {
            Gold = gold,
            Level = level,
            Stage = stage,
            Hp = hp,
        };

        return new LiveFrameUpdate(state, frame, frameId);
    }

    private static CorrectionRecord DeserializeLastCorrection(FakeFileSystem fs)
    {
        fs.Lines.Should().NotBeEmpty();
        var line = fs.Lines[^1];
        return JsonSerializer.Deserialize<CorrectionRecord>(line)!;
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        private readonly object _sync = new();
        private readonly List<string> _lines = new();

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_sync)
                {
                    return _lines.ToArray();
                }
            }
        }

        public void CreateDirectory(string path)
        {
        }

        public Task AppendLineAsync(string path, string line, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _lines.Add(line);
            }

            return Task.CompletedTask;
        }

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                return Task.FromResult<string?>(string.Join("\n", _lines));
            }
        }
    }
}