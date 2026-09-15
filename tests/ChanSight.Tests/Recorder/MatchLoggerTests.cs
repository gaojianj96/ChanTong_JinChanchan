using System.Text.Json;
using ChanSight.Recorder.Services;
using FluentAssertions;

namespace ChanSight.Tests.Recorder;

public sealed class MatchLoggerTests
{
    [Fact]
    public async Task AppendAsync_WritesOneLinePerNonDuplicateSeq()
    {
        var fs = new FakeFileSystem();
        var logger = new MatchLogger(fs);
        const string gameId = "g1";

        await logger.AppendAsync(Event(1, gameId));
        await logger.AppendAsync(Event(2, gameId));
        await logger.AppendAsync(Event(3, gameId));
        await logger.FlushAsync();

        Lines(fs, gameId).Should().HaveCount(3);
    }

    [Fact]
    public async Task AppendAsync_DuplicateSeq_IsIgnored()
    {
        var fs = new FakeFileSystem();
        var logger = new MatchLogger(fs);
        const string gameId = "g1";

        await logger.AppendAsync(Event(1, gameId));
        await logger.AppendAsync(Event(1, gameId));
        await logger.AppendAsync(Event(2, gameId));
        await logger.FlushAsync();

        Lines(fs, gameId).Select(Seq).Should().Equal(1L, 2L);
    }

    [Fact]
    public async Task AppendAsync_UnderThreshold_DoesNotWriteUntilFlush()
    {
        var fs = new FakeFileSystem();
        var logger = new MatchLogger(fs);
        const string gameId = "g1";
        var path = Path.Combine("manifests", gameId, "events.jsonl");

        await logger.AppendAsync(Event(1, gameId));
        await logger.AppendAsync(Event(2, gameId));

        fs.WrittenFiles.Should().NotContainKey(path);

        await logger.FlushAsync();

        fs.WrittenFiles.Should().ContainKey(path);
    }

    [Fact]
    public async Task AppendAsync_OverInjectedThreshold_FlushesAutomatically()
    {
        var fs = new FakeFileSystem();
        var logger = new MatchLogger(fs, flushThresholdBytes: 1);
        const string gameId = "g1";
        var path = Path.Combine("manifests", gameId, "events.jsonl");

        await logger.AppendAsync(Event(1, gameId));

        fs.WrittenFiles.Should().ContainKey(path);
        Lines(fs, gameId).Select(Seq).Should().Equal(1L);
    }

    [Fact]
    public async Task Reopening_WithSameFileSystem_ContinuesFromMaxSeqPlusOne()
    {
        var fs = new FakeFileSystem();
        const string gameId = "g1";

        var first = new MatchLogger(fs);
        await first.AppendAsync(Event(1, gameId));
        await first.AppendAsync(Event(2, gameId));
        await first.AppendAsync(Event(3, gameId));
        await first.FlushAsync();

        var second = new MatchLogger(fs);
        await second.AppendAsync(Event(2, gameId));
        await second.AppendAsync(Event(4, gameId));
        await second.FlushAsync();

        Lines(fs, gameId).Select(Seq).Should().Equal(1L, 2L, 3L, 4L);
    }

    [Fact]
    public async Task WrittenLines_AreDeserializable()
    {
        var fs = new FakeFileSystem();
        var logger = new MatchLogger(fs);
        const string gameId = "g1";

        await logger.AppendAsync(Event(1, gameId, MatchEventType.Recognition, "{\"x\":1}"));
        await logger.AppendAsync(Event(2, gameId, MatchEventType.Decision, "{\"x\":2}"));
        await logger.FlushAsync();

        var events = Lines(fs, gameId)
            .Select(line => JsonSerializer.Deserialize<MatchEvent>(line)!)
            .ToList();

        events.Should().HaveCount(2);
        events[0].Seq.Should().Be(1);
        events[0].Type.Should().Be(MatchEventType.Recognition);
        events[0].SchemaVersion.Should().Be("1");
        events[0].PayloadJson.Should().Be("{\"x\":1}");
        events[1].Seq.Should().Be(2);
        events[1].Type.Should().Be(MatchEventType.Decision);
    }

    [Fact]
    public async Task FirstAppend_WithMissingFile_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "chansight-matchlogger-" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new MatchLogger(new FileSystem(), tempDir);

            var act = async () => await logger.AppendAsync(Event(1, "new-game"));

            await act.Should().NotThrowAsync();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RealDisk_Reopen_ResumesMaxSeq()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "chansight-matchlogger-" + Guid.NewGuid().ToString("N"));
        try
        {
            const string gameId = "g1";

            var first = new MatchLogger(new FileSystem(), tempDir);
            await first.AppendAsync(Event(1, gameId));
            await first.AppendAsync(Event(2, gameId));
            await first.FlushAsync();

            var second = new MatchLogger(new FileSystem(), tempDir);
            await second.AppendAsync(Event(2, gameId));
            await second.AppendAsync(Event(3, gameId));
            await second.AppendAsync(Event(4, gameId));
            await second.FlushAsync();

            var path = Path.Combine(tempDir, gameId, "events.jsonl");
            var lines = await File.ReadAllLinesAsync(path);
            lines.Select(Seq).Should().Equal(1L, 2L, 3L, 4L);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static MatchEvent Event(
        long seq,
        string gameId,
        MatchEventType type = MatchEventType.System,
        string payload = "{}")
    {
        return new MatchEvent
        {
            Seq = seq,
            Timestamp = DateTimeOffset.UtcNow,
            GameId = gameId,
            Type = type,
            PayloadJson = payload
        };
    }

    private static string[] Lines(FakeFileSystem fs, string gameId)
    {
        var path = Path.Combine("manifests", gameId, "events.jsonl");
        fs.WrittenFiles.Should().ContainKey(path);
        return fs.WrittenFiles[path].TrimEnd('\n').Split('\n');
    }

    private static long Seq(string line)
    {
        using var doc = JsonDocument.Parse(line);
        return doc.RootElement.GetProperty("Seq").GetInt64();
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public HashSet<string> CreatedDirectories { get; } = new();

        public Dictionary<string, string> WrittenFiles { get; } = new();

        public bool DirectoryExists(string path) => CreatedDirectories.Contains(path);

        public void CreateDirectory(string path)
        {
            CreatedDirectories.Add(path);
        }

        public bool FileExists(string path) => WrittenFiles.ContainsKey(path);

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(WrittenFiles.TryGetValue(path, out var text) ? (string?)text : null);
        }

        public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WrittenFiles[path] = contents;
            return Task.CompletedTask;
        }

        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            WrittenFiles[path] = $"<{bytes.Length} bytes>";
            return Task.CompletedTask;
        }

        public Task AppendAllLinesAsync(string path, IEnumerable<string> lines, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var materialized = lines.ToList();
            if (materialized.Count == 0)
            {
                return Task.CompletedTask;
            }

            var appended = string.Join('\n', materialized) + "\n";
            WrittenFiles[path] = WrittenFiles.TryGetValue(path, out var existing) ? existing + appended : appended;
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
            => new List<string>();
    }
}