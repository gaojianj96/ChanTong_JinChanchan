using ChanSight.Vision.Models;

namespace ChanSight.Vision.Interfaces;

public interface IDatasetCleanerService
{
    Task<CleanReport> CleanDatasetAsync(string inputDirectory, string outputDirectory, DatasetCleanOptions? options = null, CancellationToken cancellationToken = default);
}