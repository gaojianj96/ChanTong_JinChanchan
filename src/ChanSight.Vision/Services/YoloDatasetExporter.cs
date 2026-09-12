using System.Text.Json;
using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;

namespace ChanSight.Vision.Services;

public sealed class YoloDatasetExporter : IYoloDatasetExporter
{
    public async Task<YoloExportResult> ExportDatasetAsync(string cleanedDirectory, string exportDirectory, YoloExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cleanedDirectory);
        ArgumentNullException.ThrowIfNull(exportDirectory);
        options ??= new YoloExportOptions();

        if (!Directory.Exists(cleanedDirectory))
            throw new DirectoryNotFoundException($"Cleaned directory not found: {cleanedDirectory}");

        var imageFiles = Directory.GetFiles(cleanedDirectory)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext is ".png" or ".jpg" or ".jpeg" or ".bmp";
            })
            .OrderBy(Path.GetFileName)
            .ToArray();

        var random = new Random(options.RandomSeed);
        var shuffled = imageFiles.OrderBy(_ => random.Next()).ToArray();

        var splitIndex = (int)(shuffled.Length * options.TrainRatio);
        var trainFiles = shuffled.Take(splitIndex).ToArray();
        var valFiles = shuffled.Skip(splitIndex).ToArray();

        var imagesTrainDir = Path.Combine(exportDirectory, "images", "train");
        var imagesValDir = Path.Combine(exportDirectory, "images", "val");
        var labelsTrainDir = Path.Combine(exportDirectory, "labels", "train");
        var labelsValDir = Path.Combine(exportDirectory, "labels", "val");

        Directory.CreateDirectory(imagesTrainDir);
        Directory.CreateDirectory(imagesValDir);
        Directory.CreateDirectory(labelsTrainDir);
        Directory.CreateDirectory(labelsValDir);

        await CopyFilesAsync(trainFiles, imagesTrainDir, labelsTrainDir, cancellationToken);
        await CopyFilesAsync(valFiles, imagesValDir, labelsValDir, cancellationToken);

        await WriteDataYamlAsync(exportDirectory, options, cancellationToken);

        return new YoloExportResult
        {
            ExportDirectory = exportDirectory,
            TrainImages = trainFiles.Length,
            ValImages = valFiles.Length,
            TotalImages = shuffled.Length,
        };
    }

    private static async Task CopyFilesAsync(string[] files, string imagesDir, string labelsDir, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            var destImage = Path.Combine(imagesDir, fileName);
            File.Copy(file, destImage, overwrite: true);

            var labelFileName = Path.GetFileNameWithoutExtension(fileName) + ".txt";
            var destLabel = Path.Combine(labelsDir, labelFileName);
            await File.WriteAllTextAsync(destLabel, string.Empty, cancellationToken);
        }
    }

    private static async Task WriteDataYamlAsync(string exportDirectory, YoloExportOptions options, CancellationToken cancellationToken)
    {
        var dataYamlPath = Path.Combine(exportDirectory, "data.yaml");

        using var writer = new StringWriter();
        writer.WriteLine($"path: {exportDirectory.Replace('\\', '/')}");
        writer.WriteLine("train: images/train");
        writer.WriteLine("val: images/val");
        writer.WriteLine();
        writer.WriteLine($"nc: {options.ClassNames.Count}");
        writer.Write("names: [");
        var namesList = string.Join(", ", Enumerable.Range(0, options.ClassNames.Count)
            .Select(i => $"'{options.ClassNames.GetValueOrDefault(i, $"class_{i}")}'"));
        writer.Write(namesList);
        writer.WriteLine("]");

        await File.WriteAllTextAsync(dataYamlPath, writer.ToString(), cancellationToken);
    }
}