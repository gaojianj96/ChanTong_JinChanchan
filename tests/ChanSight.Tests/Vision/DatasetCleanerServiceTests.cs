using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class DatasetCleanerServiceTests : IDisposable
{
    private readonly PerceptualHashService _hashService = new();
    private readonly string _tempRoot;

    public DatasetCleanerServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ChanSight_CleanerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task CleanDatasetAsync_NullInputDirectory_ThrowsArgumentNullException()
    {
        var service = new DatasetCleanerService(_hashService);
        var outputDir = CreateTempDir("output");

        var act = async () => await service.CleanDatasetAsync(null!, outputDir);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CleanDatasetAsync_NullOutputDirectory_ThrowsArgumentNullException()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");

        var act = async () => await service.CleanDatasetAsync(inputDir, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CleanDatasetAsync_NonExistentInputDirectory_ThrowsDirectoryNotFoundException()
    {
        var service = new DatasetCleanerService(_hashService);
        var outputDir = CreateTempDir("output");

        var act = async () => await service.CleanDatasetAsync(Path.Combine(_tempRoot, "nonexistent"), outputDir);

        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task CleanDatasetAsync_EmptyDirectory_ReturnsReportWithZeroCounts()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = CreateTempDir("output");

        var report = await service.CleanDatasetAsync(inputDir, outputDir);

        report.TotalFrames.Should().Be(0);
        report.EffectiveFrames.Should().Be(0);
        report.DuplicatesFiltered.Should().Be(0);
        report.BadFramesFiltered.Should().Be(0);
        report.InputDirectory.Should().Be(inputDir);
        report.OutputDirectory.Should().Be(outputDir);
    }

    [Fact]
    public async Task CleanDatasetAsync_WritesCleanReportJson()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = CreateTempDir("output");
        CreateTestImage(inputDir, "frame_001.png");

        await service.CleanDatasetAsync(inputDir, outputDir);

        var reportPath = Path.Combine(outputDir, "clean_report.json");
        File.Exists(reportPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(reportPath);
        json.Should().Contain("TotalFrames");
        json.Should().Contain("EffectiveFrames");
    }

    [Fact]
    public async Task CleanDatasetAsync_SingleGoodFrame_CopiesToOutput()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = CreateTempDir("output");
        CreateTestImage(inputDir, "frame_001.png");

        var report = await service.CleanDatasetAsync(inputDir, outputDir);

        report.TotalFrames.Should().Be(1);
        report.EffectiveFrames.Should().Be(1);
        File.Exists(Path.Combine(outputDir, "frame_001.png")).Should().BeTrue();
    }

    [Fact]
    public async Task CleanDatasetAsync_MultipleGoodFrames_CopiesAllToOutput()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = CreateTempDir("output");
        CreateTestImage(inputDir, "frame_001.png");
        CreateTestImage(inputDir, "frame_002.png");
        CreateTestImage(inputDir, "frame_003.png");

        var report = await service.CleanDatasetAsync(inputDir, outputDir);

        report.TotalFrames.Should().Be(3);
        report.EffectiveFrames.Should().Be(3);
        File.Exists(Path.Combine(outputDir, "frame_001.png")).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "frame_002.png")).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "frame_003.png")).Should().BeTrue();
    }

    [Fact]
    public async Task CleanDatasetAsync_CreatesOutputDirectory()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = Path.Combine(_tempRoot, "output_new");
        CreateTestImage(inputDir, "frame_001.png");

        await service.CleanDatasetAsync(inputDir, outputDir);

        Directory.Exists(outputDir).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "frame_001.png")).Should().BeTrue();
    }

    [Fact]
    public async Task CleanDatasetAsync_DuplicateFrames_FiltersThem()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = CreateTempDir("output");
        CreateSolidImage(inputDir, "frame_001.png", Scalar.Red);
        CreateSolidImage(inputDir, "frame_002.png", Scalar.Red);
        CreateSolidImage(inputDir, "frame_003.png", Scalar.Blue);

        var report = await service.CleanDatasetAsync(inputDir, outputDir, new DatasetCleanOptions
        {
            HammingDistanceThreshold = 1,
            VarianceThreshold = 0
        });

        report.EffectiveFrames.Should().BeLessThan(3);
        report.DuplicatesFiltered.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CleanDatasetAsync_RespectsCancellationToken()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = CreateTempDir("output");
        for (int i = 1; i <= 10; i++)
            CreateTestImage(inputDir, $"frame_{i:D3}.png");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await service.CleanDatasetAsync(inputDir, outputDir, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CleanDatasetAsync_UsesDefaultOptionsWhenNull()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = CreateTempDir("output");
        CreateTestImage(inputDir, "frame_001.png");

        var report = await service.CleanDatasetAsync(inputDir, outputDir, options: null);

        report.Options.Should().NotBeNull();
        report.Options.HammingDistanceThreshold.Should().Be(5);
    }

    [Fact]
    public async Task CleanDatasetAsync_ReportHasCompletedAt()
    {
        var service = new DatasetCleanerService(_hashService);
        var inputDir = CreateTempDir("input");
        var outputDir = CreateTempDir("output");
        CreateTestImage(inputDir, "frame_001.png");

        var report = await service.CleanDatasetAsync(inputDir, outputDir);

        report.CompletedAt.Should().BeAfter(DateTime.MinValue);
    }

    private string CreateTempDir(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateTestImage(string directory, string fileName)
    {
        using var mat = CreateVariegatedImage(60, 80);
        Cv2.ImWrite(Path.Combine(directory, fileName), mat);
    }

    private static Mat CreateVariegatedImage(int height, int width)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3);
        var random = new Random();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mat.Set(y, x, new Vec3b(
                    (byte)random.Next(256),
                    (byte)random.Next(256),
                    (byte)random.Next(256)));
            }
        }
        return mat;
    }

    private static void CreateSolidImage(string directory, string fileName, Scalar color)
    {
        using var mat = new Mat(50, 50, MatType.CV_8UC3);
        mat.SetTo(color);
        Cv2.ImWrite(Path.Combine(directory, fileName), mat);
    }
}