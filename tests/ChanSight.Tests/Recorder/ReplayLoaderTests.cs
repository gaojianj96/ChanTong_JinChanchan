using System.Text.Json;
using ChanSight.Recorder.Services;
using FluentAssertions;

namespace ChanSight.Tests.Recorder;

public sealed class ReplayLoaderTests
{
    private const string FilePath = "replay/events.jsonl";
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    [Fact]
    public async Task LoadAsync_ValidFile_SortsBySeq()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, Line(Event(3)), Line(Event(1)), Line(Event(2)));
        var loader = new ReplayLoader(fs);

        var result = await loader.LoadAsync(FilePath);

        result.Events.Select(e => e.Seq).Should().Equal(1L, 2L, 3L);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_DuplicateSeq_AddsIssue()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, Line(Event(1)), Line(Event(1)), Line(Event(2)));
        var loader = new ReplayLoader(fs);

        var result = await loader.LoadAsync(FilePath);

        result.Issues.Should().Contain(i => i.Contains("Duplicate Seq 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_BadLine_SkipsAndRecordsIssue()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, Line(Event(1)), "{ not json", Line(Event(2)));
        var loader = new ReplayLoader(fs);

        var result = await loader.LoadAsync(FilePath);

        result.Events.Select(e => e.Seq).Should().Equal(1L, 2L);
        result.Issues.Should().ContainSingle(i => i.Contains("not valid JSON", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsEmptyEventsAndSingleIssue()
    {
        var loader = new ReplayLoader(new FakeFileSystem());

        var result = await loader.LoadAsync(FilePath);

        result.Events.Should().BeEmpty();
        result.Issues.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadAsync_EmptyFile_ReturnsEmptyEventsAndNoIssues()
    {
        var fs = new FakeFileSystem();
        fs.Write(FilePath, string.Empty);
        var loader = new ReplayLoader(fs);

        var result = await loader.LoadAsync(FilePath);

        result.Events.Should().BeEmpty();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTimeline_OutOfOrderSeq_ReturnsIssue()
    {
        var events = new[] { Event(1), Event(2), Event(1) };

        var ok = new ReplayLoader(new FakeFileSystem()).ValidateTimeline(events, out var issue);

        ok.Should().BeFalse();
        issue.Should().Contain("out of order", Exactly.Once());
    }

    [Fact]
    public void ValidateTimeline_SkippedSeq_ReturnsIssue()
    {
        var events = new[] { Event(1), Event(3) };

        var ok = new ReplayLoader(new FakeFileSystem()).ValidateTimeline(events, out var issue);

        ok.Should().BeFalse();
        issue.Should().Contain("gap", Exactly.Once());
    }

    [Fact]
    public void ValidateTimeline_SeqNotStartingAtOne_ReturnsIssue()
    {
        var events = new[] { Event(2), Event(3) };

        var ok = new ReplayLoader(new FakeFileSystem()).ValidateTimeline(events, out var issue);

        ok.Should().BeFalse();
        issue.Should().Contain("start at Seq 1", Exactly.Once());
    }

    [Fact]
    public void ValidateTimeline_TimestampGoingBackwards_ReturnsIssue()
    {
        var baseTime = DateTimeOffset.UtcNow;
        var events = new[]
        {
            Event(1, baseTime),
            Event(2, baseTime.AddMinutes(-1))
        };

        var ok = new ReplayLoader(new FakeFileSystem()).ValidateTimeline(events, out var issue);

        ok.Should().BeFalse();
        issue.Should().Contain("backwards", Exactly.Once());
    }

    [Fact]
    public void ValidateTimeline_ValidTimeline_ReturnsTrue()
    {
        var baseTime = DateTimeOffset.UtcNow;
        var events = new[]
        {
            Event(1, baseTime),
            Event(2, baseTime.AddSeconds(1)),
            Event(3, baseTime.AddSeconds(2))
        };

        var ok = new ReplayLoader(new FakeFileSystem()).ValidateTimeline(events, out var issue);

        ok.Should().BeTrue();
        issue.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTimeline_EmptyList_ReturnsTrue()
    {
        var ok = new ReplayLoader(new FakeFileSystem()).ValidateTimeline(Array.Empty<MatchEvent>(), out var issue);

        ok.Should().BeTrue();
        issue.Should().BeEmpty();
    }

    private static MatchEvent Event(long seq, DateTimeOffset? timestamp = null)
    {
        return new MatchEvent
        {
            Seq = seq,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            GameId = "g1",
            Type = MatchEventType.System,
            PayloadJson = "{}"
        };
    }

    private static string Line(MatchEvent e) => JsonSerializer.Serialize(e, SerializerOptions);

    private sealed class FakeFileSystem : IFileSystem
    {
        public Dictionary<string, string> WrittenFiles { get; } = new();

        public void Write(string path, params string[] lines)
        {
            WrittenFiles[path] = string.Join('\n', lines);
        }

        public bool DirectoryExists(string path) => false;

        public void CreateDirectory(string path)
        {
        }

        public bool FileExists(string path) => WrittenFiles.ContainsKey(path);

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(WrittenFiles.TryGetValue(path, out var text) ? (string?)text : null);
        }

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
            => new List<string>();

        public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        {
            WrittenFiles[path] = contents;
            return Task.CompletedTask;
        }

        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            WrittenFiles[path] = string.Empty;
            return Task.CompletedTask;
        }

        public Task AppendAllLinesAsync(string path, IEnumerable<string> lines, CancellationToken cancellationToken = default)
        {
            WrittenFiles[path] = string.Join('\n', lines) + "\n";
            return Task.CompletedTask;
        }
    }
}
