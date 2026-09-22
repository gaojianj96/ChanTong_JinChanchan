using System.Text.Json;
using ChanSight.Core.Engine;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class RecognitionLoggerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "ChanSightTests", "recognition", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LogVlmRecognition_WritesStageGoldAndIssues_ReadableBack()
    {
        var logger = new RecognitionLogger(_tempDir);

        logger.LogVlmRecognition("m1", "manual-vlm", CreateVlmFrame(), new[] { "issue-a" });

        var line = ReadSingle(logger.GetVlmLogPath("m1"));
        line.GetProperty("type").GetString().Should().Be("vlm");
        line.GetProperty("stage").GetString().Should().Be("3-2");
        line.GetProperty("gold").GetInt32().Should().Be(47);
        line.GetProperty("level").GetInt32().Should().Be(6);
        line.GetProperty("hp").GetInt32().Should().Be(72);
        line.GetProperty("source").GetString().Should().Be("manual-vlm");
        line.GetProperty("issues").EnumerateArray().Select(e => e.GetString()).Should().Contain("issue-a");
    }

    [Fact]
    public void LogFrameRecognition_WritesFrameIdVersionAndSource()
    {
        var logger = new RecognitionLogger(_tempDir);

        logger.LogFrameRecognition("m1", "frame-1", 42, CreateSnapshot(gold: 30, stage: "4-1"), "auto");

        var line = ReadSingle(logger.GetFrameLogPath("m1"));
        line.GetProperty("type").GetString().Should().Be("frame");
        line.GetProperty("frameId").GetString().Should().Be("frame-1");
        line.GetProperty("snapshotVersion").GetInt64().Should().Be(42);
        line.GetProperty("source").GetString().Should().Be("auto");
        line.GetProperty("stage").GetString().Should().Be("4-1");
        line.GetProperty("gold").GetInt32().Should().Be(30);
    }

    [Fact]
    public void LogFrameRecognition_AppendsMultipleLines_InOrder()
    {
        var logger = new RecognitionLogger(_tempDir);

        logger.LogFrameRecognition("m1", "frame-1", 1, CreateSnapshot(), "auto");
        logger.LogFrameRecognition("m1", "frame-2", 2, CreateSnapshot(), "auto");
        logger.LogFrameRecognition("m1", "frame-3", 3, CreateSnapshot(), "auto");

        var lines = File.ReadAllLines(logger.GetFrameLogPath("m1"));
        lines.Should().HaveCount(3);

        var ids = lines.Select(l => Parse(l).GetProperty("frameId").GetString()).ToArray();
        ids.Should().Equal("frame-1", "frame-2", "frame-3");
    }

    [Fact]
    public void LogVlmRecognition_FakeFrame_DoesNotThrow()
    {
        var logger = new RecognitionLogger(_tempDir);
        var fake = new RecognitionFrame
        {
            SourceTier = SourceTier.T3,
            Stage = string.Empty,
            BoardCells = Array.Empty<UnitCell>(),
            BenchCells = Array.Empty<UnitCell>(),
            Issues = Array.Empty<string>(),
        };

        var act = () => logger.LogVlmRecognition("m1", "manual-vlm", fake, Array.Empty<string>());

        act.Should().NotThrow();
        File.Exists(logger.GetVlmLogPath("m1")).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static JsonElement ReadSingle(string path)
    {
        File.Exists(path).Should().BeTrue();
        return Parse(File.ReadAllLines(path).Single());
    }

    private static JsonElement Parse(string line)
    {
        using var doc = JsonDocument.Parse(line);
        return doc.RootElement.Clone();
    }

    private static RecognitionFrame CreateVlmFrame() => new()
    {
        SourceTier = SourceTier.T2,
        Confidence = 0.9,
        Gold = 47,
        Level = 6,
        Stage = "3-2",
        Hp = 72,
        Exp = 12,
        PlayerName = "测试玩家",
        Perspective = "self",
        BoardCells = new[]
        {
            new UnitCell("亚索", 2, new[] { new ItemStack("无尽之刃", 1) }, 0.9, SourceTier.T2),
        },
        BenchCells = new[]
        {
            new UnitCell("拉克丝", 1, Array.Empty<ItemStack>(), 0.9, SourceTier.T2),
        },
        Issues = new[] { "issue-a" },
    };

    private static GameStateSnapshot CreateSnapshot(int gold = 10, string stage = "2-1") => new(
        Stage: stage,
        Phase: GamePhase.Planning,
        Gold: gold,
        Level: 1,
        Exp: 0,
        Hp: 100,
        Streak: 0,
        PlayerName: null,
        BoardUnits: Array.Empty<BoardUnitState>(),
        BenchUnits: Array.Empty<BoardUnitState>(),
        ShopCards: Array.Empty<ShopCardState>(),
        Opponents: Array.Empty<OpponentSnapshot>(),
        Version: 0);
}
