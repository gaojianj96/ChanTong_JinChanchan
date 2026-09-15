namespace ChanSight.Vision.Services;

/// <summary>
/// Thrown when the VLM endpoint cannot be reached or returns a non-success
/// status. Callers (recognition adapters) degrade per-cell instead of crashing.
/// </summary>
public sealed class VlmUnavailableException : Exception
{
    public VlmUnavailableException()
    {
    }

    public VlmUnavailableException(string message)
        : base(message)
    {
    }

    public VlmUnavailableException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}