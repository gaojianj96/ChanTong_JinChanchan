namespace ChanSight.Vision.Interfaces;

using ChanSight.Vision.Models;

public interface IPostProcessor<TDetection> where TDetection : IDetection
{
    IReadOnlyList<TDetection> Process(IReadOnlyList<TDetection> candidates, PostProcessOptions options, CancellationToken cancellationToken = default);
}