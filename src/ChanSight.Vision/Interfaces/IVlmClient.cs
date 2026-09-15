namespace ChanSight.Vision.Interfaces;

/// <summary>
/// Minimal VLM text-completion contract. Returns the raw assistant text for a
/// multimodal prompt (text + images). Implementations must be network-backed;
/// tests substitute a fake that returns preset JSON.
/// </summary>
public interface IVlmClient
{
    Task<string> CompleteAsync(
        string prompt,
        IReadOnlyList<(string mime, byte[] data)> images,
        CancellationToken ct);
}