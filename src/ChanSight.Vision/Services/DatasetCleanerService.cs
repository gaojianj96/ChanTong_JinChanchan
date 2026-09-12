using System.Text.Json;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using OpenCvSharp;

namespace ChanSight.Vision.Services;

public sealed class DatasetCleanerService : IDatasetCleanerService
{
    private readonly IPerceptualHashService _hashService;

    public DatasetCleanerService(IPerceptualHashService hashService)
    {
        _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
    }

    public async Task<CleanReport> CleanDatasetAsync(string inputDirectory, string outputDirectory, DatasetCleanOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputDirectory);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        options ??= new DatasetCleanOptions();

        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Input directory not found: {inputDirectory}");

        var report = new CleanReport
        {
            InputDirectory = inputDirectory,
            OutputDirectory = outputDirectory,
            Options = options,
        };

        var imageFiles = options.ImageExtensions
            .SelectMany(ext => Directory.GetFiles(inputDirectory, $"*{ext}", SearchOption.TopDirectoryOnly))
            .OrderBy(Path.GetFileName)
            .ToList();

        report.TotalFrames = imageFiles.Count;

        Directory.CreateDirectory(outputDirectory);

        var keptFiles = new List<string>();
        ulong? previousHash = null;

        foreach (var file in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var mat = Cv2.ImRead(file, ImreadModes.Color);

            if (mat.Empty())
            {
                report.BadFramesFiltered++;
                continue;
            }

            if (IsBadFrame(mat, options.VarianceThreshold))
            {
                report.BadFramesFiltered++;
                continue;
            }

            var currentHash = _hashService.ComputeDHash(mat);

            if (previousHash.HasValue)
            {
                var distance = _hashService.CalculateHammingDistance(previousHash.Value, currentHash);
                if (distance <= options.HammingDistanceThreshold)
                {
                    report.DuplicatesFiltered++;
                    continue;
                }
            }

            previousHash = currentHash;
            keptFiles.Add(file);
            report.EffectiveFrames++;

            var destPath = Path.Combine(outputDirectory, Path.GetFileName(file));
            File.Copy(file, destPath, overwrite: true);
        }

        var reportPath = Path.Combine(outputDirectory, "clean_report.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(reportPath, json, cancellationToken);

        report.CompletedAt = DateTime.UtcNow;
        return report;
    }

    private static bool IsBadFrame(Mat image, double varianceThreshold)
    {
        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.MeanStdDev(gray, out var mean, out var stddev);
        var variance = stddev.Val0 * stddev.Val0;
        return variance < varianceThreshold;
    }
}