using ChanSight.Core.Engine;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class PhaseDetectorTests
{
    private static RecognitionFrame Frame(
        string stage = "3-2",
        IReadOnlyList<ShopCard>? shop = null,
        GamePhase phase = GamePhase.Planning) => new()
    {
        Stage = stage,
        ShopCards = shop ?? Array.Empty<ShopCard>(),
        Phase = phase,
    };

    private static IReadOnlyList<ShopCard> ShopWithCard() =>
        new[] { new ShopCard("Soraka", 4, 0.9, SourceTier.T1) };

    [Fact]
    public void Detect_ShopVisible_ReturnsPlanning()
    {
        var detector = new PhaseDetector();

        var phase = detector.Detect(Frame(shop: ShopWithCard()));

        phase.Should().Be(GamePhase.Planning);
    }

    [Fact]
    public void Detect_EmptyShopAndPveRound_ReturnsPve()
    {
        var detector = new PhaseDetector();

        var phase = detector.Detect(Frame(stage: "1-1"));

        phase.Should().Be(GamePhase.PvE);
    }

    [Fact]
    public void Detect_EmptyShopAndNonPveRound_ReturnsCombat()
    {
        var detector = new PhaseDetector();

        var phase = detector.Detect(Frame(stage: "3-2"));

        phase.Should().Be(GamePhase.Combat);
    }

    [Fact]
    public void Detect_StageCarouselKeyword_ReturnsCarousel()
    {
        var detector = new PhaseDetector();

        var phase = detector.Detect(Frame(stage: "2-4 选秀"));

        phase.Should().Be(GamePhase.Carousel);
    }

    [Fact]
    public void Detect_UpstreamAlreadySetToValidPhase_ReturnsIt()
    {
        var detector = new PhaseDetector();

        var phase = detector.Detect(Frame(shop: ShopWithCard(), phase: GamePhase.Carousel));

        phase.Should().Be(GamePhase.Carousel);
    }

    [Fact]
    public void Detect_ShopCardWithCostOnly_ReturnsPlanning()
    {
        var detector = new PhaseDetector();
        var shop = new[] { new ShopCard(string.Empty, 3, 0.5, SourceTier.T3) };

        var phase = detector.Detect(Frame(shop: shop));

        phase.Should().Be(GamePhase.Planning);
    }

    [Fact]
    public void Detect_NullFrame_ThrowsArgumentNullException()
    {
        var detector = new PhaseDetector();

        var act = () => detector.Detect(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DetectAsync_VlmReturnsCarousel_ReturnsCarousel()
    {
        var vlm = new FakeAdvisorVlmClient().QueueResult("""{"phase":"Carousel"}""");
        var detector = new PhaseDetector(vlm);

        var phase = await detector.DetectAsync(Frame());

        phase.Should().Be(GamePhase.Carousel);
    }

    [Fact]
    public async Task DetectAsync_VlmReturnsInvalid_AllBackToHeuristic()
    {
        var vlm = new FakeAdvisorVlmClient().QueueResult("""{"phase":"Teleport"}""");
        var detector = new PhaseDetector(vlm);

        var phase = await detector.DetectAsync(Frame(stage: "1-1"));

        phase.Should().Be(GamePhase.PvE);
    }

    [Fact]
    public async Task DetectAsync_VlmThrows_FallsBackToHeuristicWithoutThrowing()
    {
        var vlm = new FakeAdvisorVlmClient().QueueException(new InvalidOperationException("boom"));
        var detector = new PhaseDetector(vlm);

        var phase = await detector.DetectAsync(Frame(shop: ShopWithCard()));

        phase.Should().Be(GamePhase.Planning);
    }

    [Fact]
    public async Task DetectAsync_NullFrame_ThrowsArgumentNullException()
    {
        var detector = new PhaseDetector();

        var act = () => detector.DetectAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void TryParsePhase_ValidJson_Parses()
    {
        var ok = PhaseDetector.TryParsePhase("""{"phase":"PvE"}""", out var phase);

        ok.Should().BeTrue();
        phase.Should().Be(GamePhase.PvE);
    }

    [Fact]
    public void TryParsePhase_InvalidValue_ReturnsFalse()
    {
        var ok = PhaseDetector.TryParsePhase("""{"phase":"not-a-phase"}""", out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void TryParsePhase_NotJson_ReturnsFalse()
    {
        var ok = PhaseDetector.TryParsePhase("Planning", out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void TryParsePhase_Null_ReturnsFalse()
    {
        var ok = PhaseDetector.TryParsePhase(null, out _);

        ok.Should().BeFalse();
    }
}