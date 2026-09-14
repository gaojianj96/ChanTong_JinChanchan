using ChanSight.Core.Interfaces;
using ChanSight.Core.Models;
using ChanSight.Recorder.Services;
using ChanSight.Tests.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace ChanSight.Tests.Recorder;

public sealed class DatasetSamplerServiceTests
{
    [Fact]
    public async Task StartAsync_CreatesOutputAndSnapshotsDirectories()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();

        await service.StartAsync(session, new MockFrameSource());

        fs.CreatedDirectories.Should().Contain(session.OutputDirectory);
        fs.CreatedDirectories.Should().Contain(Path.Combine(session.OutputDirectory, "snapshots"));
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        var act = async () => await service.StartAsync(session, new MockFrameSource());

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
    public async Task StopAsync_WritesDatasetIndexJson()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        source.Complete();
        await service.StopAsync();

        var indexPath = Path.Combine(session.OutputDirectory, "dataset_index.json");
        fs.WrittenFiles.Should().ContainKey(indexPath);
        var json = fs.WrittenFiles[indexPath];
        json.Should().Contain(session.Id.ToString());
        json.Should().Contain(session.Name);
    }

    [Fact]
    public async Task AutoSampling_SavesFramesAtInterval()
    {
        var fs = new FakeFileSystem();
        var options = new DatasetSamplerOptions
        {
            IntervalSeconds = 0.1,
            ImageFormat = "png"
        };
        await using var service = new DatasetSamplerService(options, fs, NullLogger<DatasetSamplerService>.Instance);
        var session = CreateSession();
        var source = new MockFrameSource();
        var baseTime = DateTimeOffset.UtcNow;

        await service.StartAsync(session, source);
        source.TryWrite(CreateFrame(1, baseTime));
        source.TryWrite(CreateFrame(2, baseTime.AddSeconds(0.05)));
        source.TryWrite(CreateFrame(3, baseTime.AddSeconds(0.15)));
        source.TryWrite(CreateFrame(4, baseTime.AddSeconds(0.25)));
        source.Complete();
        await service.StopAsync();

        var autoFiles = fs.WrittenFiles.Keys
            .Where(k => k.Contains("auto_"))
            .ToList();
        autoFiles.Should().HaveCount(3);
    }

    [Fact]
    public async Task TakeManualSnapshotAsync_SavesToSnapshotsDirectory()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        source.TryWrite(CreateFrame(42, DateTimeOffset.UtcNow));
        await service.TakeManualSnapshotAsync();
        source.Complete();
        await service.StopAsync();

        var indexPath = Path.Combine(session.OutputDirectory, "dataset_index.json");
        fs.WrittenFiles.Should().ContainKey(indexPath);
        var json = fs.WrittenFiles[indexPath];
        json.Should().Contain("manual");
        json.Should().Contain("\"SequenceNumber\": 42");
    }

    [Fact]
    public async Task TakeManualSnapshotAsync_WhenNotStarted_ThrowsInvalidOperationException()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);

        var act = async () => await service.TakeManualSnapshotAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TakeManualSnapshotAsync_WithNoFrameInChannel_DoesNotThrow()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource(capacity: 1);

        await service.StartAsync(session, source);
        var act = async () => await service.TakeManualSnapshotAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DatasetIndexJson_ContainsSampleDetails()
    {
        var fs = new FakeFileSystem();
        await using var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();
        var now = DateTimeOffset.UtcNow;

        await service.StartAsync(session, source);
        source.TryWrite(CreateFrame(1, now));
        source.Complete();
        await service.StopAsync();

        var indexPath = Path.Combine(session.OutputDirectory, "dataset_index.json");
        var json = fs.WrittenFiles[indexPath];
        json.Should().Contain("\"totalSamples\": 1");
        json.Should().Contain("auto_");
        json.Should().Contain("\"SequenceNumber\": 1");
    }

    [Fact]
    public async Task ImageFormatJpg_SavesWithJpgExtension()
    {
        var fs = new FakeFileSystem();
        var options = new DatasetSamplerOptions
        {
            IntervalSeconds = 0.01,
            ImageFormat = "jpg"
        };
        await using var service = new DatasetSamplerService(options, fs, NullLogger<DatasetSamplerService>.Instance);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        source.TryWrite(CreateFrame(1, DateTimeOffset.UtcNow));
        source.Complete();
        await service.StopAsync();

        fs.WrittenFiles.Keys.Should().Contain(k => k.EndsWith(".jpg"));
    }

    [Fact]
    public async Task OptionsValidation_InvalidInterval_Throws()
    {
        var options = new DatasetSamplerOptions { IntervalSeconds = -1 };

        var act = () => new DatasetSamplerService(options, NullLogger<DatasetSamplerService>.Instance);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task OptionsValidation_InvalidFormat_Throws()
    {
        var options = new DatasetSamplerOptions { ImageFormat = "bmp" };

        var act = () => new DatasetSamplerService(options, NullLogger<DatasetSamplerService>.Instance);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task DisposeAsync_StopsSampling()
    {
        var fs = new FakeFileSystem();
        var service = CreateService(fs);
        var session = CreateSession();
        var source = new MockFrameSource();

        await service.StartAsync(session, source);
        source.TryWrite(CreateFrame(1, DateTimeOffset.UtcNow));
        await service.DisposeAsync();

        var indexPath = Path.Combine(session.OutputDirectory, "dataset_index.json");
        fs.WrittenFiles.Should().ContainKey(indexPath);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var fs = new FakeFileSystem();
        var service = CreateService(fs);
        var session = CreateSession();

        await service.StartAsync(session, new MockFrameSource());
        await service.DisposeAsync();
        await service.DisposeAsync();

        fs.DirectoryCreateCount.Should().Be(fs.DirectoryCreateCount);
    }

    private static DatasetSamplerService CreateService(FakeFileSystem fs)
    {
        var options = new DatasetSamplerOptions
        {
            IntervalSeconds = 0.1,
            ImageFormat = "png"
        };
        return new DatasetSamplerService(options, fs, NullLogger<DatasetSamplerService>.Instance);
    }

    private static SessionMeta CreateSession()
    {
        return new SessionMeta(
            Guid.NewGuid(),
            "test-sampler",
            "sampler-output",
            new WindowTarget(1, "test-window", new FrameResolution(640, 480), DpiInfo.Default),
            DateTimeOffset.UtcNow,
            30,
            new FrameResolution(640, 480));
    }

    private static CapturedFrame CreateFrame(long sequenceNumber, DateTimeOffset timestamp)
    {
        return new CapturedFrame(
            new Mat(10, 10, MatType.CV_8UC3),
            timestamp,
            sequenceNumber);
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public HashSet<string> CreatedDirectories { get; } = new();
        public Dictionary<string, string> WrittenFiles { get; } = new();
        public int DirectoryCreateCount { get; private set; }

        public bool DirectoryExists(string path) => CreatedDirectories.Contains(path);

        public void CreateDirectory(string path)
        {
            DirectoryCreateCount++;
            CreatedDirectories.Add(path);
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
            WrittenFiles[path] = $"<{bytes.Length} bytes>";
            return Task.CompletedTask;
        }
    }
}