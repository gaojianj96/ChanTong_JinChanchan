using ChanSight.Core.Engine;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class ManualFrameVlmServiceTests
{
    private static Mat CreateFrame() => new(1920, 1080, MatType.CV_8UC3, Scalar.All(128));

    private static ManualFrameVlmService CreateService(FakeVlmClient fake) =>
        new(fake, new RoiMapperService());

    [Fact]
    public async Task RecognizeAsync_SelfValidJson_MapsFieldsCorrectly()
    {
        const string json = """
            {
              "perspective": "self",
              "opponentIndex": null,
              "playerName": "测试玩家",
              "stage": "3-2",
              "hp": 72,
              "level": 6,
              "exp": 12,
              "gold": 47,
              "goldEstimate": null,
              "streak": 2,
              "board": [ { "slot": 0, "hero": "亚索", "star": 2, "items": ["无尽之刃"] } ],
              "bench": [ { "slot": 0, "hero": "拉克丝", "star": 1, "items": [] } ],
              "confidence": 0.9
            }
            """;
        var fake = new FakeVlmClient().QueueResult(json);
        var service = CreateService(fake);

        var frame = await service.RecognizeAsync(CreateFrame(), isSelf: true);

        frame.Gold.Should().Be(47);
        frame.Stage.Should().Be("3-2");
        frame.Hp.Should().Be(72);
        frame.Level.Should().Be(6);
        frame.Exp.Should().Be(12);
        frame.PlayerName.Should().Be("测试玩家");
        frame.OpponentIndex.Should().BeNull();
        frame.GoldEstimate.Should().BeNull();
        frame.Perspective.Should().Be("self");
        frame.SourceTier.Should().Be(SourceTier.T2);

        frame.BoardCells.Should().HaveCount(1);
        frame.BoardCells[0].Name.Should().Be("亚索");
        frame.BoardCells[0].Star.Should().Be(2);
        frame.BoardCells[0].Items.Should().HaveCount(1);
        frame.BoardCells[0].Items[0].IconId.Should().Be("无尽之刃");

        frame.BenchCells.Should().HaveCount(1);
        frame.BenchCells[0].Name.Should().Be("拉克丝");

        frame.Issues.Should().NotBeNull();
    }

    [Fact]
    public async Task RecognizeAsync_OpponentFrame_ThenApplyOpponent_MapsIntoOpponentSnapshot()
    {
        const string json = """
            {
              "perspective": "opponent",
              "opponentIndex": 2,
              "playerName": "对手乙",
              "stage": "3-2",
              "hp": 55,
              "level": 5,
              "exp": 8,
              "gold": 30,
              "goldEstimate": 30,
              "streak": 0,
              "board": [ { "slot": 0, "hero": "盖伦", "star": 3, "items": ["狂徒铠甲"] } ],
              "bench": [ { "slot": 0, "hero": "赵信", "star": 1, "items": [] } ],
              "confidence": 0.7
            }
            """;
        var fake = new FakeVlmClient().QueueResult(json);
        var service = CreateService(fake);

        var frame = await service.RecognizeAsync(CreateFrame(), isSelf: false);

        frame.OpponentIndex.Should().Be(2);
        frame.PlayerName.Should().Be("对手乙");
        frame.GoldEstimate.Should().Be(30);
        frame.Perspective.Should().Be("opponent");

        var manager = new GameStateManager();
        var adapter = new RecognitionToGameStateAdapter(manager);
        adapter.ApplyOpponent(frame, 2);

        var opponent = manager.Current.Opponents[2];
        opponent.PlayerName.Should().Be("对手乙");
        opponent.GoldEstimate.Should().Be(30);
        opponent.BoardUnits.Should().HaveCount(1);
        opponent.BoardUnits[0].Name.Should().Be("盖伦");
        opponent.BoardUnits[0].Star.Should().Be(3);
        opponent.BoardUnits[0].Items.Should().Contain("狂徒铠甲");
        opponent.BenchUnits.Should().HaveCount(1);
        opponent.BenchUnits[0].Name.Should().Be("赵信");
    }

    [Fact]
    public async Task RecognizeAsync_MissingAndInvalidFields_NullsAndRecordsIssuesWithoutThrowing()
    {
        const string json = """
            {
              "perspective": "self",
              "stage": "3-2",
              "board": [
                { "slot": 0, "hero": "不存在的英雄", "star": 2, "items": [] },
                { "slot": 1, "hero": "亚索", "star": 9, "items": ["神秘装备"] }
              ]
            }
            """;
        var fake = new FakeVlmClient().QueueResult(json);
        var service = CreateService(fake);

        var frame = await service.RecognizeAsync(CreateFrame(), isSelf: true);

        frame.Hp.Should().Be(0);
        frame.Level.Should().Be(0);
        frame.Gold.Should().Be(0);
        frame.PlayerName.Should().BeNull();

        frame.BoardCells.Should().HaveCount(2);
        frame.BoardCells[0].Name.Should().BeNull();
        frame.BoardCells[1].Star.Should().Be(0);

        frame.Issues.Should().NotBeEmpty();
        frame.Issues.Should().Contain(i => i.Contains("不存在的英雄"));
        frame.Issues.Should().Contain(i => i.Contains("星级"));
    }

    [Fact]
    public async Task RecognizeAsync_ClientThrowsVlmUnavailable_PropagatesWithoutCrashing()
    {
        var fake = new FakeVlmClient().QueueException(new VlmUnavailableException("endpoint down"));
        var service = CreateService(fake);

        Func<Task> act = () => service.RecognizeAsync(CreateFrame(), isSelf: true);

        await act.Should().ThrowAsync<VlmUnavailableException>();
    }

    [Fact]
    public async Task RecognizeAsync_PromptContainsHeroAndItemDictionaries()
    {
        var fake = new FakeVlmClient().QueueResult("{}");
        var service = CreateService(fake);

        await service.RecognizeAsync(CreateFrame(), isSelf: true);

        fake.LastPrompt.Should().NotBeNull();
        fake.LastPrompt!.Should().Contain(GameSeasonDictionary.Heroes.First());
        fake.LastPrompt.Should().Contain(GameSeasonDictionary.Items.First());
        fake.LastPrompt.Should().Contain("JSON");
    }

    [Fact]
    public async Task RecognizeAsync_SelfSendsMultipleCrops()
    {
        var fake = new FakeVlmClient().QueueResult("{}");
        var service = CreateService(fake);

        await service.RecognizeAsync(CreateFrame(), isSelf: true);

        fake.LastImages.Should().NotBeNull();
        fake.LastImages!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RecognizeAsync_OpponentSendsOpponentsSidebarCrop()
    {
        var fake = new FakeVlmClient().QueueResult("{}");
        var service = CreateService(fake);

        await service.RecognizeAsync(CreateFrame(), isSelf: false);

        fake.LastImages.Should().NotBeNull();
        fake.LastImages!.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    private sealed class FakeVlmClient : IVlmClient
    {
        private readonly Queue<object> _script = new();

        public string? LastPrompt { get; private set; }
        public IReadOnlyList<(string mime, byte[] data)>? LastImages { get; private set; }

        public FakeVlmClient QueueResult(string json)
        {
            _script.Enqueue(json);
            return this;
        }

        public FakeVlmClient QueueException(Exception exception)
        {
            _script.Enqueue(exception);
            return this;
        }

        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct)
        {
            LastPrompt = prompt;
            LastImages = images;

            var next = _script.Count > 0
                ? _script.Dequeue()
                : throw new InvalidOperationException("FakeVlmClient script exhausted.");

            if (next is Exception exception)
                throw exception;

            return Task.FromResult((string)next);
        }
    }
}