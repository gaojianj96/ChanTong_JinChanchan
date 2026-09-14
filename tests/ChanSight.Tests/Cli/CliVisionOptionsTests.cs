namespace ChanSight.Tests.Cli;

using ChanSight.Cli;
using FluentAssertions;

public sealed class CliVisionOptionsTests
{
    [Fact]
    public void EmptyArgs_AllDefaults()
    {
        var options = CliVisionOptions.Parse(Array.Empty<string>());

        options.VisionDryRun.Should().BeFalse();
        options.ProbeModelPath.Should().BeNull();
    }

    [Fact]
    public void DryRunFlag_Parses()
    {
        var options = CliVisionOptions.Parse(new[] { "--vision-dry-run" });

        options.VisionDryRun.Should().BeTrue();
        options.ProbeModelPath.Should().BeNull();
    }

    [Fact]
    public void ProbeFlag_WithPath_Parses()
    {
        var options = CliVisionOptions.Parse(new[] { "--probe", "model.onnx" });

        options.ProbeModelPath.Should().Be("model.onnx");
        options.VisionDryRun.Should().BeFalse();
    }

    [Fact]
    public void UnknownArgs_Ignored()
    {
        var options = CliVisionOptions.Parse(new[] { "--foo", "bar" });

        options.VisionDryRun.Should().BeFalse();
        options.ProbeModelPath.Should().BeNull();
    }
}