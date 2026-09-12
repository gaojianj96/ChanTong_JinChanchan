using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests;

public sealed class OpenCvRuntimeTests
{
    [Fact]
    public void OpenCvSharpRuntime_LoadsVersionString()
    {
        Cv2.GetVersionString().Should().NotBeNullOrWhiteSpace();
    }
}
