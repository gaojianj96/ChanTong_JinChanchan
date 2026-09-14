using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using ChanSight.Recorder.Services;
using ChanSight.Tests.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace ChanSight.Tests.Recorder;

public sealed class VideoRecorderServiceTests
{
    [Fact]
    public async Task StartAsync_SetsIsRecordingAndCreatesOutputDirectory()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);

        service.IsRecording.Should().BeTrue();
        service.IsPaused.Should().BeFalse();
        service.CurrentSession.Should().Be(session);
        fs.CreatedDirectories.Should().Contain(session.OutputDirectory);
    }

    [Fact]
    public async Task StopAsync_StopsRecordingAndWritesMetaJson()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        source.Complete();
        await service.StopAsync();

        service.IsRecording.Should().BeFalse();
        service.CurrentSession.Should().BeNull();
        fs.WrittenFiles.Should().ContainKey(Path.Combine(session.OutputDirectory, "meta.json"));
    }

    [Fact]
    public async Task PauseAsync_WhenRecording_SetsIsPaused()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var source = new MockFrameSource();

        await service.StartAsync(CreateSession(), source);
        await service.PauseAsync();

        service.IsPaused.Should().BeTrue();
    }

    [Fact]
    public async Task PauseAsync_WhenNotRecording_ThrowsInvalidOperationException()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);

        var act = async () => await service.PauseAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResumeAsync_AfterPause_ClearsIsPaused()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var source = new MockFrameSource();

        await service.StartAsync(CreateSession(), source);
        await service.PauseAsync();
        await service.ResumeAsync();

        service.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task ResumeAsync_WhenNotRecording_ThrowsInvalidOperationException()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);

        var act = async () => await service.ResumeAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRecording_ThrowsInvalidOperationException()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var source = new MockFrameSource();

        await service.StartAsync(CreateSession(), source);
        var act = async () => await service.StartAsync(CreateSession("other"), new MockFrameSource());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);

        var act = async () => await service.StopAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_IsIdempotent()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var source = new MockFrameSource();

        await service.StartAsync(CreateSession(), source);
        source.Complete();
        await service.StopAsync();
        await service.StopAsync();

        service.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task RecordFrames_WritesFrameFilesToOutputDirectory()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        source.TryWrite(CreateFrame(1));
        source.TryWrite(CreateFrame(2));
        source.Complete();
        await service.StopAsync();

        var frameFiles = fs.WrittenFiles.Keys
            .Where(k => k.StartsWith(session.OutputDirectory) && k.Contains("frame_"))
            .ToList();
        frameFiles.Should().HaveCount(2);
        frameFiles.Should().Contain(k => k.EndsWith("frame_00000001.png"));
        frameFiles.Should().Contain(k => k.EndsWith("frame_00000002.png"));
    }

    [Fact]
    public async Task Frames_AllWritten_WhenNotPaused()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        source.TryWrite(CreateFrame(1));
        source.TryWrite(CreateFrame(2));
        source.TryWrite(CreateFrame(3));
        source.TryWrite(CreateFrame(4));
        source.Complete();
        await service.StopAsync();

        var frameFiles = fs.WrittenFiles.Keys
            .Where(k => k.StartsWith(session.OutputDirectory) && k.Contains("frame_"))
            .ToList();
        frameFiles.Should().HaveCount(4);
    }

    [Fact]
    public async Task Pause_SkipsFrames_AndResumeContinues_Real()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        await service.PauseAsync();

        source.TryWrite(CreateFrame(1));
        source.TryWrite(CreateFrame(2));
        await Task.Delay(200);

        await service.ResumeAsync();
        source.TryWrite(CreateFrame(3));
        source.TryWrite(CreateFrame(4));
        source.Complete();
        await service.StopAsync();

        var frameFiles = fs.WrittenFiles.Keys
            .Where(k => k.StartsWith(session.OutputDirectory) && k.Contains("frame_"))
            .ToList();
        frameFiles.Should().HaveCount(2);
        frameFiles.Should().Contain(k => k.EndsWith("frame_00000001.png"));
        frameFiles.Should().Contain(k => k.EndsWith("frame_00000002.png"));
    }

    [Fact]
    public async Task PauseAsync_ThenResumeAsync_TogglesPausedState()
    {
        await using var service = CreateService(new FakeFileSystem());
        var source = new MockFrameSource();

        await service.StartAsync(CreateSession(), source);

        service.IsPaused.Should().BeFalse();

        await service.PauseAsync();
        service.IsPaused.Should().BeTrue();

        await service.ResumeAsync();
        service.IsPaused.Should().BeFalse();

        source.Complete();
        await service.StopAsync();
    }

    [Fact]
    public async Task MetaJson_HasCorrectStructure()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        source.Complete();
        await service.StopAsync();

        var metaPath = Path.Combine(session.OutputDirectory, "meta.json");
        fs.WrittenFiles.Should().ContainKey(metaPath);
        var metaJson = fs.WrittenFiles[metaPath];
        metaJson.Should().Contain(session.Id.ToString());
        metaJson.Should().Contain(session.Name);
        metaJson.Should().Contain(session.Target.Title);
        metaJson.Should().Contain(session.Resolution.Width.ToString());
        metaJson.Should().Contain(session.Resolution.Height.ToString());
    }

    [Fact]
    public async Task DisposeAsync_StopsRecording()
    {
        var fs = new FakeFileSystem();
        var service = CreateService(fs);
        var source = new MockFrameSource();

        await service.StartAsync(CreateSession(), source);
        source.TryWrite(CreateFrame(1));
        await service.DisposeAsync();

        var metaPath = Path.Combine("output", "meta.json");
        fs.WrittenFiles.Should().ContainKey(metaPath);
    }

    private static VideoRecorderService CreateService(IFileSystem fs)
        => new(fs, NullLogger<VideoRecorderService>.Instance);

    private static SessionMeta CreateSession(string? name = null)
    {
        return new SessionMeta(
            Guid.NewGuid(),
            name ?? "test-session",
            "output",
            new WindowTarget(1, "test-window", new FrameResolution(640, 480), DpiInfo.Default),
            DateTimeOffset.UtcNow,
            30,
            new FrameResolution(640, 480));
    }

    private static CapturedFrame CreateFrame(long sequenceNumber)
    {
        return new CapturedFrame(
            new Mat(10, 10, MatType.CV_8UC3),
            DateTimeOffset.UtcNow,
            sequenceNumber);
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public HashSet<string> CreatedDirectories { get; } = new();
        public Dictionary<string, string> WrittenFiles { get; } = new();
        public List<string> DirectoryFiles { get; } = new();

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(WrittenFiles.TryGetValue(path, out var text) ? (string?)text : null);
        }

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
        {
            if (DirectoryFiles.Count == 0)
            {
                return new List<string>();
            }

            var prefix = searchPattern.Replace("*.png", string.Empty).Replace("*", string.Empty);
            return DirectoryFiles
                .Where(f => f.StartsWith(prefix, StringComparison.Ordinal) && f.EndsWith(".png", StringComparison.Ordinal))
                .ToList();
        }

        public bool DirectoryExists(string path) => CreatedDirectories.Contains(path);

        public void CreateDirectory(string path)
        {
            CreatedDirectories.Add(path);
        }

        public bool FileExists(string path) => WrittenFiles.ContainsKey(path);

        public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        {
            WrittenFiles[path] = contents;
            return Task.CompletedTask;
        }

        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            WrittenFiles[path] = $"<{bytes.Length} bytes>";
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task FrameWriteFailure_StopStillWritesMetaAndClearsState()
    {
        var throwing = new ThrowingFileSystem { FramesBeforeThrow = 2 };
        await using var service = CreateService(throwing);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);

        for (var i = 0; i < 6; i++)
        {
            source.TryWrite(CreateFrame(i + 1));
        }

        await Task.Delay(200);
        await service.StopAsync();

        service.IsRecording.Should().BeFalse();
        service.CurrentSession.Should().BeNull();
        throwing.Inner.WrittenFiles.Should().ContainKey(Path.Combine(session.OutputDirectory, "meta.json"));
    }

    [Fact]
    public async Task StartAsync_WritesProvisionalMeta()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);

        fs.WrittenFiles.Should().ContainKey(Path.Combine(session.OutputDirectory, "meta.json"));
    }

    private sealed class ThrowingFileSystem : IFileSystem
    {
        private readonly FakeFileSystem _inner = new();
        public int FramesBeforeThrow { get; init; } = 0;
        public int FrameWrites { get; private set; }
        public FakeFileSystem Inner => _inner;

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public void CreateDirectory(string path) => _inner.CreateDirectory(path);

        public bool FileExists(string path) => _inner.FileExists(path);

        public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
            => _inner.WriteAllTextAsync(path, contents, cancellationToken);

        public Task<string?> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
            => _inner.ReadAllTextAsync(path, cancellationToken);

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
            => _inner.EnumerateFiles(directory, searchPattern);

        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (path.EndsWith(".png", StringComparison.Ordinal))
            {
                FrameWrites++;
                if (FrameWrites > FramesBeforeThrow)
                {
                    throw new IOException("disk full");
                }
            }

            return _inner.WriteAllBytesAsync(path, bytes, cancellationToken);
        }
    }

    [Fact]
    public async Task OrphanProvisionalMeta_IsArchivedOnRestartWithoutFrames()
    {
        var fs = new FakeFileSystem();
        var session = CreateSession();
        var metaPath = Path.Combine(session.OutputDirectory, "meta.json");
        fs.WrittenFiles[metaPath] = "{\"provisional\": true}";

        var source = new MockFrameSource();
        await using var service = CreateService(fs);
        await service.StartAsync(session, source, CancellationToken.None);

        fs.WrittenFiles.Should().ContainKey(metaPath + ".orphan");
    }

    [Fact]
    public async Task StartWithExistingFrames_DoesNotSweepMeta()
    {
        var fs = new FakeFileSystem();
        var session = CreateSession();
        var metaPath = Path.Combine(session.OutputDirectory, "meta.json");
        fs.WrittenFiles[metaPath] = "{\"provisional\": true}";
        fs.DirectoryFiles.Add("frame_00000001.png");

        var source = new MockFrameSource();
        await using var service = CreateService(fs);
        await service.StartAsync(session, source, CancellationToken.None);

        fs.WrittenFiles.Keys.Should().NotContain(k => k.EndsWith(".orphan", StringComparison.Ordinal));
    }
}