using System.Text.Json;
using ChanSight.Core.Annotation;
using ChanSight.Core.Engine;
using ChanSight.Overlay.Models;
using ChanSight.Overlay.ViewModels;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Overlay;

public sealed class ManualVlmViewModelTests
{
    [Fact]
    public async Task RunManualRecognition_Self_UpdatesFieldsAndMarksCalibrated()
    {
        var manager = new GameStateManager();
        var vm = CreateViewModel(
            manager,
            (isSelf, _) => Task.FromResult(SelfFrame(gold: 52, level: 7, stage: "3-2", hp: 63)));

        await vm.RunManualRecognitionAsync(isSelf: true);

        vm.Gold.Should().Be(52);
        vm.Level.Should().Be(7);
        vm.Stage.Should().Be("3-2");
        vm.Hp.Should().Be(63);
        vm.DataHealth.Should().Be(DataHealth.Calibrated);
        vm.HealthLabel.Should().Be("已校准");

        // 己方识别应经 adapter 写入 GameStateManager。
        manager.Current.Gold.Should().Be(52);
    }

    [Fact]
    public async Task OpponentRecognition_ConfirmIndex_WritesOpponentSnapshot()
    {
        var manager = new GameStateManager();
        var vm = CreateViewModel(
            manager,
            (isSelf, _) => Task.FromResult(
                new RecognitionFrame
                {
                    Perspective = "opponent",
                    OpponentIndex = 2,
                    PlayerName = "对手乙",
                    GoldEstimate = 30,
                }));

        await vm.RunManualRecognitionAsync(isSelf: false);
        vm.OpponentIndex.Should().Be(2);

        vm.ConfirmOpponentCommand.Execute(null);

        var opponent = manager.Current.Opponents[2];
        opponent.PlayerName.Should().Be("对手乙");
        opponent.GoldEstimate.Should().Be(30);
    }

    [Fact]
    public async Task CommitItemsCorrection_EntersStickyAndCorrectionRecord()
    {
        var fs = new FakeFileSystem();
        var vm = CreateViewModel(new GameStateManager(), null, fs);
        vm.ApplyUpdate(CreateUpdate(frameId: "f1"));
        vm.PinNow();

        await vm.CommitItemsCorrectionAsync(RegionTypes.BoardItems, 0, new[] { "羊刀", "狂徒铠甲" });

        // CorrectionRecord 携带 Items 字段。
        var record = DeserializeLastCorrection(fs);
        record.RegionType.Should().Be(RegionTypes.BoardItems);
        using (var doc = JsonDocument.Parse(record.CorrectedValue))
        {
            var items = doc.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(i => i.GetString())
                .ToArray();
            items.Should().Contain("羊刀");
            items.Should().Contain("狂徒铠甲");
        }

        // 粘滞修正叠加进重算快照。
        var corrected = vm.BuildCorrectedSnapshot();
        corrected.BoardUnits[0].Items.Should().Contain("羊刀");
        corrected.BoardUnits[0].Items.Should().Contain("狂徒铠甲");
    }

    [Fact]
    public async Task DataHealth_AutoIsUncalibrated_ManualIsCalibrated()
    {
        var vm = CreateViewModel(
            new GameStateManager(),
            (isSelf, _) => Task.FromResult(SelfFrame()));

        // 自动识别产物 → 未校准。
        vm.ApplyUpdate(CreateUpdate(frameId: "auto-1"));
        vm.DataHealth.Should().Be(DataHealth.Uncalibrated);
        vm.IsUncalibrated.Should().BeTrue();

        // 手动 VLM 识别 → 已校准。
        await vm.RunManualRecognitionAsync(isSelf: true);
        vm.DataHealth.Should().Be(DataHealth.Calibrated);
        vm.IsUncalibrated.Should().BeFalse();
    }

    [Fact]
    public async Task ManualRecognition_WithIssues_StatusPromptsManualReview()
    {
        var vm = CreateViewModel(
            new GameStateManager(),
            (isSelf, _) => Task.FromResult(
                SelfFrame() with { Issues = new[] { "board 应为 28 项, 实际 1 项。" } }));

        await vm.RunManualRecognitionAsync(isSelf: true);

        vm.Status.Should().Contain("核对");
    }

    [Fact]
    public async Task OpponentRecognition_UnknownIndex_DoesNotWriteAndPromptsConfirm()
    {
        var manager = new GameStateManager();
        var vm = CreateViewModel(
            manager,
            (isSelf, _) => Task.FromResult(
                new RecognitionFrame
                {
                    Perspective = "opponent",
                    OpponentIndex = null,
                    PlayerName = "对手丙",
                    GoldEstimate = 20,
                }));

        await vm.RunManualRecognitionAsync(isSelf: false);
        vm.IsOpponentIndexUnknown.Should().BeTrue();

        vm.ConfirmOpponentCommand.Execute(null);

        // 未知序号不写槽: 对手快照保持初始 PlayerName null。
        manager.Current.Opponents.Select(o => o.PlayerName).Should().OnlyContain(name => name == null);
        vm.Status.Should().Contain("请先选择对手序号");
    }

    private static LiveViewModel CreateViewModel(
        GameStateManager manager,
        ManualRecognizeFunc? recognize,
        FakeFileSystem? fs = null)
    {
        fs ??= new FakeFileSystem();
        var store = new AnnotationStore(fs, "corrections");
        var adapter = new RecognitionToGameStateAdapter(manager);

        Func<GameStateSnapshot, DecisionPanelResult> evaluate = state => new DecisionPanelResult(
            state,
            Array.Empty<TacticalRecommendation>(),
            AdvisorAdvice: null);

        return new LiveViewModel(store, evaluate, service: null, manualRecognize: recognize, adapter: adapter);
    }

    private static RecognitionFrame SelfFrame(int gold = 0, int level = 1, string stage = "1-1", int hp = 100) =>
        new()
        {
            Perspective = "self",
            Gold = gold,
            Level = level,
            Stage = stage,
            Hp = hp,
        };

    private static LiveFrameUpdate CreateUpdate(string frameId)
    {
        var state = new GameStateSnapshot(
            Stage: "1-1",
            Phase: GamePhase.Planning,
            Gold: 0,
            Level: 1,
            Exp: 0,
            Hp: 100,
            Streak: 0,
            PlayerName: null,
            BoardUnits: Enumerable.Range(0, 28).Select(i => new BoardUnitState(i, i == 0 ? "盖伦" : null, i == 0 ? 1 : 0, i == 0 ? 1 : 0, Array.Empty<string>())).ToArray(),
            BenchUnits: Enumerable.Range(0, 9).Select(i => new BoardUnitState(i, null, 0, 0, Array.Empty<string>())).ToArray(),
            ShopCards: Enumerable.Range(0, 5).Select(i => new ShopCardState(i, null, 0)).ToArray(),
            Opponents: Array.Empty<OpponentSnapshot>(),
            Version: 1);

        return new LiveFrameUpdate(state, new RecognitionFrame(), frameId);
    }

    private static CorrectionRecord DeserializeLastCorrection(FakeFileSystem fs)
    {
        fs.Lines.Should().NotBeEmpty();
        return JsonSerializer.Deserialize<CorrectionRecord>(fs.Lines[^1])!;
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