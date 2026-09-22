using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class VlmRecognitionAdapterTests
{
    [Fact]
    public async Task RecognizeAsync_ValidJson_ParsesAndFuzzyCorrectsNames()
    {
        const string json = """
            [
              { "cellIndex": 0, "name": "亚索", "star": 2, "confidence": 0.95 },
              { "cellIndex": 5, "name": "阿离", "star": 3, "confidence": 0.62 },
              { "cellIndex": 9, "name": "噬金兽", "star": 1, "confidence": 0.4 }
            ]
            """;
        var fake = new FakeVlmClient().QueueResult(json);
        var adapter = new VlmRecognitionAdapter(fake);
        var cells = CreateCells(0, 5, 9);

        var result = await adapter.RecognizeAsync(cells);

        result.Verdicts.Should().HaveCount(3);

        result.Verdicts[0].CellIndex.Should().Be(0);
        result.Verdicts[0].Name.Should().Be("亚索");
        result.Verdicts[0].Star.Should().Be(2);
        result.Verdicts[0].Confidence.Should().BeApproximately(0.95, 1e-9);
        result.Verdicts[0].SourceTier.Should().Be(SourceTier.T2);

        // Near-dictionary name is corrected.
        result.Verdicts[1].Name.Should().Be("阿狸");

        // Unknown name that is not in the dictionary is kept verbatim.
        result.Verdicts[2].Name.Should().Be("噬金兽");
        result.Verdicts[2].Star.Should().Be(1);
    }

    [Fact]
    public async Task RecognizeAsync_InvalidJson_DegradesAllCellsWithoutThrowing()
    {
        var fake = new FakeVlmClient().QueueResult("this is not json {{");
        var adapter = new VlmRecognitionAdapter(fake);
        var cells = CreateCells(0, 1, 2);

        var result = await adapter.RecognizeAsync(cells);

        result.Verdicts.Should().HaveCount(3);
        result.Verdicts.Should().OnlyContain(v => v.Name == null && v.Star == null && v.Confidence == 0.0);
    }

    [Fact]
    public async Task RecognizeAsync_ClientThrowsVlmUnavailable_DegradesAllCells()
    {
        var fake = new FakeVlmClient()
            .QueueException(new VlmUnavailableException("endpoint down"))
            .QueueException(new VlmUnavailableException("endpoint down"));
        var adapter = new VlmRecognitionAdapter(fake);
        var cells = CreateCells(0, 5);

        var result = await adapter.RecognizeAsync(cells);

        result.Verdicts.Should().HaveCount(2);
        result.Verdicts.Should().OnlyContain(v => v.Name == null && v.Confidence == 0.0);
        fake.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task RecognizeAsync_PromptContainsFullHeroTable()
    {
        var fake = new FakeVlmClient().QueueResult("[]");
        var adapter = new VlmRecognitionAdapter(fake);
        var cells = CreateCells(3);

        await adapter.RecognizeAsync(cells);

        fake.LastPrompt.Should().NotBeNull();
        fake.LastPrompt!.Should().Contain(GameSeasonDictionary.Heroes.First());
        fake.LastPrompt.Should().Contain("JSON");
    }

    [Fact]
    public async Task RecognizeAsync_FirstAttemptFailsThenSucceeds_RetriesOnce()
    {
        const string json = """
            [ { "cellIndex": 7, "name": "盖伦", "star": 1, "confidence": 0.88 } ]
            """;
        var fake = new FakeVlmClient()
            .QueueException(new VlmUnavailableException("transient"))
            .QueueResult(json);
        var adapter = new VlmRecognitionAdapter(fake);
        var cells = CreateCells(7);

        var result = await adapter.RecognizeAsync(cells);

        fake.CallCount.Should().Be(2);
        result.Verdicts.Should().HaveCount(1);
        result.Verdicts[0].CellIndex.Should().Be(7);
        result.Verdicts[0].Name.Should().Be("盖伦");
        result.Verdicts[0].Star.Should().Be(1);
    }

    [Fact]
    public async Task RecognizeAsync_ClientThrowsUnavailableEveryCall_CapsTotalRequestsAtTwo()
    {
        var fake = new AlwaysThrowingVlmClient();
        var adapter = new VlmRecognitionAdapter(fake);
        var cells = CreateCells(0, 1, 2);

        var result = await adapter.RecognizeAsync(cells);

        // Single retry in the adapter, no retry in the client: total requests === 2.
        fake.CallCount.Should().Be(2);
        result.Verdicts.Should().HaveCount(3);
        result.Verdicts.Should().OnlyContain(v => v.Name == null && v.Confidence == 0.0);
    }

    [Fact]
    public async Task RecognizeAsync_HungClient_BoundedTimeout_DegradesWithoutBlockingOrThrowing()
    {
        var hung = new HangingVlmClient();
        var adapter = new VlmRecognitionAdapter(hung, timeout: TimeSpan.FromMilliseconds(50));
        var cells = CreateCells(0, 5, 9);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await adapter.RecognizeAsync(cells, CancellationToken.None);
        sw.Stop();

        // A hung VLM (e.g. an upstream 60s timeout) must degrade promptly instead of
        // stalling the live frame loop or propagating a cancellation.
        result.Verdicts.Should().HaveCount(3);
        result.Verdicts.Should().OnlyContain(v => v.Name == null && v.Confidence == 0.0);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    private static IReadOnlyList<VlmCellInput> CreateCells(params int[] indices) =>
        indices.Select(i => new VlmCellInput(i, new byte[] { 1, 2, 3 }, "image/png")).ToArray();

    /// <summary>
    /// <see cref="IVlmClient"/> that throws <see cref="VlmUnavailableException"/> on
    /// every call, used to prove the adapter never exceeds two total requests.
    /// </summary>
    private sealed class AlwaysThrowingVlmClient : IVlmClient
    {
        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct)
        {
            CallCount++;
            throw new VlmUnavailableException("endpoint down");
        }
    }

    /// <summary>
    /// <see cref="IVlmClient"/> that never completes, used to prove the auto-path
    /// budget degrades instead of stalling on a hung/upstream-timeout VLM. Honour the
    /// cancellation token so a budget expiry releases the awaited call.
    /// </summary>
    private sealed class HangingVlmClient : IVlmClient
    {
        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => tcs.TrySetCanceled(ct));
            return tcs.Task;
        }
    }

    /// <summary>
    /// Programmable in-memory <see cref="IVlmClient"/> that replays queued
    /// results/exceptions and records the last prompt/images. Never touches the network.
    /// </summary>
    private sealed class FakeVlmClient : IVlmClient
    {
        private readonly Queue<object> _script = new();

        public string? LastPrompt { get; private set; }
        public IReadOnlyList<(string mime, byte[] data)>? LastImages { get; private set; }
        public int CallCount { get; private set; }

        public FakeVlmClient QueueResult(string json)
        {
            _script.Enqueue(json);
            return this;
        }

        public FakeVlmClient QueueException(Exception exception)
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
                : throw new InvalidOperationException("FakeVlmClient script exhausted.");

            if (next is Exception exception)
                throw exception;

            return Task.FromResult((string)next);
        }
    }
}