using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class FusionArbitratorTests
{
    [Fact]
    public void Fuse_LocalPreferred_StarAndNameFromT0()
    {
        var arbitrator = new FusionArbitrator(new FakeVlmClient());
        var locals = new[] { new LocalCellResult(0, 2, true) };
        var vlmVerdicts = new[] { new VlmCellVerdict(0, "盖伦", 3, 0.9) };

        var result = arbitrator.Fuse(locals, vlmVerdicts);

        result.Should().HaveCount(1);
        result[0].CellIndex.Should().Be(0);
        result[0].Star.Should().Be(2);
        result[0].Name.Should().Be("盖伦");
        result[0].SourceTier.Should().Be(SourceTier.T0);
        result[0].EscalationLevel.Should().Be(0);
    }

    [Fact]
    public void Fuse_VlmHighConfidence_FillsNameWhenLocalEmpty()
    {
        var arbitrator = new FusionArbitrator(new FakeVlmClient());
        var locals = new[] { new LocalCellResult(1, 0, false) };
        var vlmVerdicts = new[] { new VlmCellVerdict(1, "亚索", 3, 0.9) };

        var result = arbitrator.Fuse(locals, vlmVerdicts);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("亚索");
        result[0].Confidence.Should().BeApproximately(0.9, 1e-9);
    }

    [Fact]
    public void Fuse_VlmLowConfidenceOnEmpty_DoesNotBackfillName()
    {
        var arbitrator = new FusionArbitrator(new FakeVlmClient());
        var locals = new[] { new LocalCellResult(2, 0, false) };
        var vlmVerdicts = new[] { new VlmCellVerdict(2, "盖伦", 1, 0.3) };

        var result = arbitrator.Fuse(locals, vlmVerdicts);

        result.Should().HaveCount(1);
        result[0].Name.Should().BeNull();
    }

    [Fact]
    public async Task FuseWithEscalationAsync_FirstLowThenHighConfidence_RecordsT2()
    {
        var fake = new FakeVlmClient()
            .QueueResult(VerdictJson((5, "盖伦", 0.1)))
            .QueueResult(VerdictJson((5, "盖伦", 0.9)));
        var arbitrator = new FusionArbitrator(fake);
        var locals = new[] { new LocalCellResult(5, 0, false) };
        var crops = new[] { new VlmCellInput(5, new byte[] { 1 }, "image/png") };

        var result = await arbitrator.FuseWithEscalationAsync(locals, crops);

        result.Should().HaveCount(1);
        result[0].CellIndex.Should().Be(5);
        result[0].Name.Should().Be("盖伦");
        result[0].EscalationLevel.Should().Be(2);
        result[0].SourceTier.Should().Be(SourceTier.T2);
        fake.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task FuseWithEscalationAsync_AlwaysLowConfidence_ThirdLevelWithNullName()
    {
        var fake = new FakeVlmClient()
            .QueueResult(VerdictJson((7, "盖伦", 0.1)))
            .QueueResult(VerdictJson((7, "盖伦", 0.1)));
        var arbitrator = new FusionArbitrator(fake);
        var locals = new[] { new LocalCellResult(7, 0, false) };
        var crops = new[] { new VlmCellInput(7, new byte[] { 1 }, "image/png") };

        var result = await arbitrator.FuseWithEscalationAsync(locals, crops);

        result.Should().HaveCount(1);
        result[0].EscalationLevel.Should().Be(3);
        result[0].SourceTier.Should().Be(SourceTier.T3);
        result[0].Name.Should().BeNull();
        result[0].Confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task FuseWithEscalationAsync_LocalDetermined_StaysAtT0WithoutVlmCall()
    {
        var fake = new FakeVlmClient();
        var arbitrator = new FusionArbitrator(fake);
        var locals = new[] { new LocalCellResult(3, 2, true) };
        var crops = new[] { new VlmCellInput(3, new byte[] { 1 }, "image/png") };

        var result = await arbitrator.FuseWithEscalationAsync(locals, crops);

        result.Should().HaveCount(1);
        result[0].EscalationLevel.Should().Be(0);
        result[0].Star.Should().Be(2);
        fake.CallCount.Should().Be(0);
    }

    [Fact]
    public void ApplyCorrection_OverridesVlmName()
    {
        var arbitrator = new FusionArbitrator(new FakeVlmClient());
        arbitrator.ApplyCorrection(3, "亚索");

        var locals = new[] { new LocalCellResult(3, 1, true) };
        var vlmVerdicts = new[] { new VlmCellVerdict(3, "盖伦", 2, 0.9) };

        var result = arbitrator.Fuse(locals, vlmVerdicts);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("亚索");
        result[0].Star.Should().Be(1);
    }

    [Fact]
    public void OutOfRangeInputs_AreIgnoredWithoutThrowing()
    {
        var arbitrator = new FusionArbitrator(new FakeVlmClient());
        var locals = new[]
        {
            new LocalCellResult(-1, 1, true),
            new LocalCellResult(42, 2, true),
            new LocalCellResult(2, 0, false),
        };
        var vlmVerdicts = new[] { new VlmCellVerdict(2, "盖伦", 1, 0.9) };

        var result = arbitrator.Fuse(locals, vlmVerdicts);

        result.Should().HaveCount(1);
        result[0].CellIndex.Should().Be(2);
    }

    [Fact]
    public void Corrections_ExposesFewShotLibrary()
    {
        var arbitrator = new FusionArbitrator(new FakeVlmClient());
        arbitrator.ApplyCorrection(4, "劫");

        arbitrator.Corrections.Should().ContainKey(4).WhoseValue.Should().Be("劫");
        arbitrator.TryGetCorrection(4, out var name).Should().BeTrue();
        name.Should().Be("劫");
    }

    [Fact]
    public void Fuse_NullInputs_ThrowArgumentNullException()
    {
        var arbitrator = new FusionArbitrator(new FakeVlmClient());

        var fuseNullLocals = () => arbitrator.Fuse(null!, Array.Empty<VlmCellVerdict>());
        var fuseNullVlm = () => arbitrator.Fuse(Array.Empty<LocalCellResult>(), null!);

        fuseNullLocals.Should().Throw<ArgumentNullException>();
        fuseNullVlm.Should().Throw<ArgumentNullException>();
    }

    private static string VerdictJson(params (int index, string name, double confidence)[] cells)
    {
        var entries = cells.Select(c =>
            $$"""{ "cellIndex": {{c.index}}, "name": "{{c.name}}", "star": 2, "confidence": {{c.confidence.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }""");
        return "[" + string.Join(", ", entries) + "]";
    }

    private sealed class FakeVlmClient : IVlmClient
    {
        private readonly Queue<string> _script = new();

        public int CallCount { get; private set; }

        public FakeVlmClient QueueResult(string json)
        {
            _script.Enqueue(json);
            return this;
        }

        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct)
        {
            CallCount++;
            var next = _script.Count > 0
                ? _script.Dequeue()
                : throw new InvalidOperationException("FakeVlmClient script exhausted.");
            return Task.FromResult(next);
        }
    }
}