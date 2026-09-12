using ChanSight.Vision.Services;
using FluentAssertions;
using OpenCvSharp;

namespace ChanSight.Tests.Vision;

public sealed class PerceptualHashServiceTests
{
    private readonly PerceptualHashService _service = new();

    [Fact]
    public void ComputeDHash_SameImage_ReturnsSameHash()
    {
        using var image = CreateSolidColorImage(100, 100, Scalar.Red);

        var hash1 = _service.ComputeDHash(image);
        var hash2 = _service.ComputeDHash(image);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeDHash_DifferentImages_ReturnsDifferentHashes()
    {
        using var image1 = CreateNonZeroHashImage(100, 100);
        using var image2 = CreateSolidColorImage(100, 100, Scalar.Red);

        var hash1 = _service.ComputeDHash(image1);
        var hash2 = _service.ComputeDHash(image2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeDHash_PatternImage_ReturnsNonZeroHash()
    {
        using var image = CreateNonZeroHashImage(100, 100);

        var hash = _service.ComputeDHash(image);

        hash.Should().NotBe(0UL);
    }

    [Fact]
    public void ComputeAHash_SameImage_ReturnsSameHash()
    {
        using var image = CreateSolidColorImage(100, 100, Scalar.Green);

        var hash1 = _service.ComputeAHash(image);
        var hash2 = _service.ComputeAHash(image);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeAHash_DifferentImages_ReturnsDifferentHashes()
    {
        using var image1 = CreateNonZeroHashImage(100, 100);
        using var image2 = CreateSolidColorImage(100, 100, Scalar.Green);

        var hash1 = _service.ComputeAHash(image1);
        var hash2 = _service.ComputeAHash(image2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void CalculateHammingDistance_IdenticalHashes_ReturnsZero()
    {
        var hash = 0xDEADBEEFCAFEBABEUL;

        var distance = _service.CalculateHammingDistance(hash, hash);

        distance.Should().Be(0);
    }

    [Fact]
    public void CalculateHammingDistance_DifferentHashes_ReturnsCorrectCount()
    {
        var distance = _service.CalculateHammingDistance(0UL, ulong.MaxValue);

        distance.Should().Be(64);
    }

    [Fact]
    public void CalculateHammingDistance_OneBitDifference_ReturnsOne()
    {
        var distance = _service.CalculateHammingDistance(0UL, 1UL);

        distance.Should().Be(1);
    }

    [Fact]
    public void ComputeDHash_SolidColorImage_ReturnsZeroHash()
    {
        using var image = CreateSolidColorImage(100, 100, Scalar.White);

        var hash = _service.ComputeDHash(image);

        hash.Should().Be(0UL);
    }

    [Fact]
    public void ComputeAHash_SolidColorImage_ReturnsZeroHash()
    {
        using var image = CreateSolidColorImage(100, 100, Scalar.White);

        var hash = _service.ComputeAHash(image);

        hash.Should().Be(0UL);
    }

    private static Mat CreateSolidColorImage(int width, int height, Scalar color)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3);
        mat.SetTo(color);
        return mat;
    }

    private static Mat CreateNonZeroHashImage(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3);
        var left = width / 3;
        var right = width * 2 / 3;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var value = (byte)(x < left ? 255 : x < right ? 0 : 255);
                mat.Set(y, x, new Vec3b(value, value, value));
            }
        }
        return mat;
    }
}