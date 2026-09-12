using ChanSight.Vision.Models;

namespace ChanSight.Vision.Interfaces;

public interface IYoloDatasetExporter
{
    Task<YoloExportResult> ExportDatasetAsync(string cleanedDirectory, string exportDirectory, YoloExportOptions? options = null, CancellationToken cancellationToken = default);
}