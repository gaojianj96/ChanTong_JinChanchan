using ChanSight.Vision.Interfaces;

namespace ChanSight.Tests.Vision;

/// <summary>
/// Programmable in-memory <see cref="IVlmClient"/> that returns a preset JSON
/// string (or throws) and records the prompt/images it received. Never touches
/// the network.
/// </summary>
public sealed class FakeAdvisorVlmClient : IVlmClient
{
    private readonly Queue<object> _script = new();

    public string? LastPrompt { get; private set; }
    public IReadOnlyList<(string mime, byte[] data)>? LastImages { get; private set; }
    public int CallCount { get; private set; }

    public FakeAdvisorVlmClient QueueResult(string json)
    {
        _script.Enqueue(json);
        return this;
    }

    public FakeAdvisorVlmClient QueueException(Exception exception)
    {
        _script.Enqueue(exception);
        return this;
    }

    public Task<string> CompleteAsync(
        string prompt,
        IReadOnlyList<(string mime, byte[] data)> images,
        CancellationToken ct)
    {
        LastPrompt = prompt;
        LastImages = images;
        CallCount++;

        var next = _script.Count > 0
            ? _script.Dequeue()
            : throw new InvalidOperationException("FakeAdvisorVlmClient script exhausted.");

        if (next is Exception exception)
            throw exception;

        return Task.FromResult((string)next);
    }
}