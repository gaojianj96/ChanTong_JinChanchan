using System.Text.Json;
using System.Text.Json.Serialization;
using ChanSight.Cli;
using ChanSight.Cli.Retrospective;
using ChanSight.Recorder.Services;
using ChanSight.Tests.Vision;
using FluentAssertions;

namespace ChanSight.Tests.Cli;

public sealed class ReplayCommandTests
{
    private const string FilePath = "replay/events.jsonl";
    private const string GameId = "match-2026-001";
    private const string Hero = "拉克丝";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task RunAsync_SyntheticEvents_PrintsSummaryAndFindingSeqRefs()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, Line(Event(1)), Line(Event(2)), Line(Event(3)));
        var vlm = new FakeAdvisorVlmClient().QueueResult(
            """{"summary":"整体运营稳健","findings":[{"claim":"第 1 回合的 roll 决策风险偏高","evidenceSeqs":[1,2],"confidence":0.8}],"nextActions":["下局优先升人口"],"confidence":0.75}""");

        var command = Command(fs, vlm);

        var output = await CaptureConsoleAsync(() => command.RunAsync(new ReplayOptions(FilePath, Export: false, ExportPath: null, Privacy: false)));

        output.Should().Contain("Summary");
        output.Should().Contain("整体运营稳健");
        output.Should().Contain("[Seq 1, 2]");
    }

    [Fact]
    public async Task RunAsync_PrivacyEnabled_RedactsGameIdAndHeroNames()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, Line(Event(1)), Line(Event(2)), Line(Event(3)));
        var vlm = new FakeAdvisorVlmClient().QueueResult(
            """{"summary":"拉克丝 carry 运营稳健","findings":[{"claim":"拉克丝装备成型","evidenceSeqs":[1,2],"confidence":0.8}],"nextActions":["继续养拉克丝"],"confidence":0.75}""");

        var exportPath = TempFile();
        var command = Command(fs, vlm);

        var output = await CaptureConsoleAsync(() => command.RunAsync(new ReplayOptions(FilePath, Export: true, ExportPath: exportPath, Privacy: true)));

        output.Should().Contain("[redacted]");
        output.Should().NotContain(GameId);
        output.Should().NotContain(Hero);

        var exported = await File.ReadAllTextAsync(exportPath);
        var model = JsonSerializer.Deserialize<ReplayExportModel>(exported, JsonOptions);
        model.Should().NotBeNull();
        model!.GameId.Should().Be("[redacted]");
        model.Report.Summary.Should().Contain("[redacted]");
        model.Report.Summary.Should().NotContain(Hero);
        model.Report.Findings.Should().OnlyContain(f => !f.Claim.Contains(Hero, StringComparison.Ordinal));
        model.Report.NextActions.Should().OnlyContain(a => !a.Contains(Hero, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_PrivacyDisabled_PreservesGameIdAndHeroNames()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, Line(Event(1)), Line(Event(2)), Line(Event(3)));
        var vlm = new FakeAdvisorVlmClient().QueueResult(
            """{"summary":"拉克丝 carry 运营稳健","findings":[{"claim":"拉克丝装备成型","evidenceSeqs":[1,2],"confidence":0.8}],"nextActions":["继续养拉克丝"],"confidence":0.75}""");

        var exportPath = TempFile();
        var command = Command(fs, vlm);

        var output = await CaptureConsoleAsync(() => command.RunAsync(new ReplayOptions(FilePath, Export: true, ExportPath: exportPath, Privacy: false)));

        output.Should().Contain(GameId);
        output.Should().Contain(Hero);

        var exported = await File.ReadAllTextAsync(exportPath);
        var model = JsonSerializer.Deserialize<ReplayExportModel>(exported, JsonOptions);
        model.Should().NotBeNull();
        model!.GameId.Should().Be(GameId);
        model.Report.Summary.Should().Contain(Hero);
        model.Report.Summary.Should().NotContain("[redacted]");
    }

    [Fact]
    public async Task RunAsync_Export_WritesDeserializableReplayExportModel()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, Line(Event(1)), Line(Event(2)), Line(Event(3)));
        var vlm = new FakeAdvisorVlmClient().QueueResult(
            """{"summary":"整体运营稳健","findings":[{"claim":"第 1 回合的 roll 决策风险偏高","evidenceSeqs":[1,2],"confidence":0.8}],"nextActions":["下局优先升人口"],"confidence":0.75}""");

        var exportPath = TempFile();
        var command = Command(fs, vlm);

        await command.RunAsync(new ReplayOptions(FilePath, Export: true, ExportPath: exportPath, Privacy: false));

        File.Exists(exportPath).Should().BeTrue();

        var model = JsonSerializer.Deserialize<ReplayExportModel>(await File.ReadAllTextAsync(exportPath), JsonOptions);
        model.Should().NotBeNull();
        model!.GameId.Should().Be(GameId);
        model.Score.Should().NotBeNull();
        model.Report.Summary.Should().Be("整体运营稳健");
        model.Report.Findings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunAsync_EmptyEvents_DoesNotThrowAndStillPrintsPanel()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, string.Empty);
        var vlm = new FakeAdvisorVlmClient().QueueResult(
            """{"summary":"无事件对局","findings":[],"nextActions":[],"confidence":0.5}""");

        var command = Command(fs, vlm);

        var output = await CaptureConsoleAsync(() => command.RunAsync(new ReplayOptions(FilePath, Export: false, ExportPath: null, Privacy: false)));

        output.Should().Contain("Summary");
        output.Should().Contain("无事件对局");
    }

    [Fact]
    public void Parse_ReplayWithExportAndPrivacy_SetsReplayOptions()
    {
        var options = CliVisionOptions.Parse(new[] { "--replay", "a.json", "--replay-export", "--replay-privacy" });

        options.Replay.Should().NotBeNull();
        options.Replay!.ReplayPath.Should().Be("a.json");
        options.Replay.Export.Should().BeTrue();
        options.Replay.ExportPath.Should().BeNull();
        options.Replay.Privacy.Should().BeTrue();
    }

    [Fact]
    public void Parse_ReplayWithExplicitExportPath_SetsExportPath()
    {
        var options = CliVisionOptions.Parse(new[] { "--replay", "a.json", "--replay-export", "out.json" });

        options.Replay.Should().NotBeNull();
        options.Replay!.Export.Should().BeTrue();
        options.Replay.ExportPath.Should().Be("out.json");
        options.Replay.Privacy.Should().BeFalse();
    }

    [Fact]
    public void Parse_ReplayOnly_ExportDefaultsToFalse()
    {
        var options = CliVisionOptions.Parse(new[] { "--replay", "a.json" });

        options.Replay.Should().NotBeNull();
        options.Replay!.Export.Should().BeFalse();
        options.Replay.Privacy.Should().BeFalse();
    }

    private static ReplayCommand Command(IFileSystem fs, FakeAdvisorVlmClient vlm)
    {
        var scorer = new RetrospectiveScorer();
        return new ReplayCommand(
            new ReplayLoader(fs),
            scorer,
            new RetrospectiveReportGenerator(vlm, scorer));
    }

    private static async Task<string> CaptureConsoleAsync(Func<Task> action)
    {
        var original = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            await action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static string TempFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "chansight-r4-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "out.report.json");
    }

    private static MatchEvent Event(long seq) => new()
    {
        Seq = seq,
        Timestamp = DateTimeOffset.UtcNow,
        GameId = GameId,
        Type = MatchEventType.System,
        PayloadJson = "{}"
    };

    private static string Line(MatchEvent e) => JsonSerializer.Serialize(e);

    private sealed class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new();

        public void Write(string path, params string[] lines)
        {
            _files[path] = string.Join('\n', lines);
        }

        public bool DirectoryExists(string path) => false;

        public void CreateDirectory(string path)
        {
        }

        public bool FileExists(string path) => _files.ContainsKey(path);

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_files.TryGetValue(path, out var text) ? (string?)text : null);
        }

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
            => new List<string>();

        public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        {
            _files[path] = contents;
            return Task.CompletedTask;
        }

        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            _files[path] = string.Empty;
            return Task.CompletedTask;
        }

        public Task AppendAllLinesAsync(string path, IEnumerable<string> lines, CancellationToken cancellationToken = default)
        {
            _files[path] = string.Join('\n', lines) + "\n";
            return Task.CompletedTask;
        }
    }
}
