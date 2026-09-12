using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class YoloDatasetExporterTests : IDisposable
{
    private readonly string _tempRoot;

    public YoloDatasetExporterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ChanSight_YoloTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task ExportDatasetAsync_NullCleanedDirectory_ThrowsArgumentNullException()
    {
        var service = new YoloDatasetExporter();
        var exportDir = CreateTempDir("export");

        var act = async () => await service.ExportDatasetAsync(null!, exportDir);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportDatasetAsync_NullExportDirectory_ThrowsArgumentNullException()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");

        var act = async () => await service.ExportDatasetAsync(cleanedDir, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportDatasetAsync_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        var service = new YoloDatasetExporter();

        var act = async () => await service.ExportDatasetAsync(Path.Combine(_tempRoot, "nonexistent"), CreateTempDir("export"));

        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task ExportDatasetAsync_CreatesYoloDirectoryStructure()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        CreateTestImage(cleanedDir, "img_001.png");

        await service.ExportDatasetAsync(cleanedDir, exportDir);

        Directory.Exists(Path.Combine(exportDir, "images", "train")).Should().BeTrue();
        Directory.Exists(Path.Combine(exportDir, "images", "val")).Should().BeTrue();
        Directory.Exists(Path.Combine(exportDir, "labels", "train")).Should().BeTrue();
        Directory.Exists(Path.Combine(exportDir, "labels", "val")).Should().BeTrue();
    }

    [Fact]
    public async Task ExportDatasetAsync_WritesDataYaml()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        CreateTestImage(cleanedDir, "img_001.png");

        await service.ExportDatasetAsync(cleanedDir, exportDir);

        var yamlPath = Path.Combine(exportDir, "data.yaml");
        File.Exists(yamlPath).Should().BeTrue();
        var yaml = await File.ReadAllTextAsync(yamlPath);
        yaml.Should().Contain("train: images/train");
        yaml.Should().Contain("val: images/val");
        yaml.Should().Contain("nc: 1");
    }

    [Fact]
    public async Task ExportDatasetAsync_SplitsTrainValByRatio()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        for (int i = 1; i <= 10; i++)
            CreateTestImage(cleanedDir, $"img_{i:D3}.png");

        var options = new YoloExportOptions { TrainRatio = 0.7, RandomSeed = 42 };
        var result = await service.ExportDatasetAsync(cleanedDir, exportDir, options);

        result.TotalImages.Should().Be(10);
        result.TrainImages.Should().Be(7);
        result.ValImages.Should().Be(3);
    }

    [Fact]
    public async Task ExportDatasetAsync_CreatesLabelFiles()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        for (int i = 1; i <= 3; i++)
            CreateTestImage(cleanedDir, $"img_{i:D3}.png");

        await service.ExportDatasetAsync(cleanedDir, exportDir, new YoloExportOptions { TrainRatio = 0.8, RandomSeed = 42 });

        var trainLabels = Directory.GetFiles(Path.Combine(exportDir, "labels", "train"));
        var valLabels = Directory.GetFiles(Path.Combine(exportDir, "labels", "val"));
        trainLabels.Should().HaveCount(2);
        valLabels.Should().HaveCount(1);
        trainLabels.Should().OnlyContain(f => f.EndsWith(".txt"));
    }

    [Fact]
    public async Task ExportDatasetAsync_RespectsRandomSeed()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir1 = CreateTempDir("export1");
        var exportDir2 = CreateTempDir("export2");
        for (int i = 1; i <= 5; i++)
            CreateTestImage(cleanedDir, $"img_{i:D3}.png");

        var options1 = new YoloExportOptions { TrainRatio = 0.8, RandomSeed = 42 };
        var options2 = new YoloExportOptions { TrainRatio = 0.8, RandomSeed = 42 };

        var result1 = await service.ExportDatasetAsync(cleanedDir, exportDir1, options1);
        var result2 = await service.ExportDatasetAsync(cleanedDir, exportDir2, options2);

        result1.TrainImages.Should().Be(result2.TrainImages);
        result1.ValImages.Should().Be(result2.ValImages);
    }

    [Fact]
    public async Task ExportDatasetAsync_EmptyDirectory_ReturnsZeroCounts()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");

        var result = await service.ExportDatasetAsync(cleanedDir, exportDir);

        result.TotalImages.Should().Be(0);
        result.TrainImages.Should().Be(0);
        result.ValImages.Should().Be(0);
    }

    [Fact]
    public async Task ExportDatasetAsync_RespectsCancellationToken()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        for (int i = 1; i <= 10; i++)
            CreateTestImage(cleanedDir, $"img_{i:D3}.png");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await service.ExportDatasetAsync(cleanedDir, exportDir, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExportDatasetAsync_UsesDefaultOptionsWhenNull()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        CreateTestImage(cleanedDir, "img_001.png");

        var result = await service.ExportDatasetAsync(cleanedDir, exportDir, options: null);

        result.ExportDirectory.Should().Be(exportDir);
        result.TotalImages.Should().Be(1);
    }

    [Fact]
    public async Task ExportDatasetAsync_AllImagesGoToTrainWhenRatioIsOne()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        for (int i = 1; i <= 5; i++)
            CreateTestImage(cleanedDir, $"img_{i:D3}.png");

        var options = new YoloExportOptions { TrainRatio = 1.0, RandomSeed = 42 };
        var result = await service.ExportDatasetAsync(cleanedDir, exportDir, options);

        result.TrainImages.Should().Be(5);
        result.ValImages.Should().Be(0);
    }

    [Fact]
    public async Task ExportDatasetAsync_FiltersNonImageFiles()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        CreateTestImage(cleanedDir, "img_001.png");
        await File.WriteAllTextAsync(Path.Combine(cleanedDir, "clean_report.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(cleanedDir, "readme.txt"), "hello");

        var result = await service.ExportDatasetAsync(cleanedDir, exportDir);

        result.TotalImages.Should().Be(1);
    }

    [Fact]
    public async Task ExportDatasetAsync_DataYamlContainsClassNames()
    {
        var service = new YoloDatasetExporter();
        var cleanedDir = CreateTempDir("cleaned");
        var exportDir = CreateTempDir("export");
        CreateTestImage(cleanedDir, "img_001.png");

        await service.ExportDatasetAsync(cleanedDir, exportDir, new YoloExportOptions
        {
            ClassNames = new Dictionary<int, string> { [0] = "champion", [1] = "minion" }
        });

        var yaml = await File.ReadAllTextAsync(Path.Combine(exportDir, "data.yaml"));
        yaml.Should().Contain("nc: 2");
        yaml.Should().Contain("champion");
        yaml.Should().Contain("minion");
    }

    private string CreateTempDir(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateTestImage(string directory, string fileName)
    {
        using var mat = new Mat(20, 30, MatType.CV_8UC3);
        var random = new Random(fileName.GetHashCode());
        mat.SetTo(new Scalar(random.Next(0, 255), random.Next(0, 255), random.Next(0, 255)));
        Cv2.ImWrite(Path.Combine(directory, fileName), mat);
    }
}