using ChanSight.Vision.Interfaces;
using ChanSight.Vision.Models;
using ChanSight.Vision.Services;
using FluentAssertions;

namespace ChanSight.Tests.Vision;

public sealed class LLMAdvisorTests
{
    [Fact]
    public async Task AdviseAsync_ValidJson_ParsesSuggestion()
    {
        const string json = """{"suggestion":"选择强化符文A","reason":"契合当前法师阵容","confidence":0.87}""";
        var fake = new FakeAdvisorVlmClient().QueueResult(json);
        var advisor = new LLMAdvisor(fake);

        var result = await advisor.AdviseAsync(new AdvisorEvent(AdvisorEventType.HexAugment, "2-1 海克斯三选一"));

        result.Suggestion.Should().NotBeNullOrWhiteSpace();
        result.Reason.Should().NotBeNullOrWhiteSpace();
        result.Confidence.Should().BeApproximately(0.87, 1e-9);
        result.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task AdviseAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        var fake = new FakeAdvisorVlmClient().QueueResult("this is not json {{");
        var advisor = new LLMAdvisor(fake);

        var act = async () => await advisor.AdviseAsync(new AdvisorEvent(AdvisorEventType.ThreeStar, "有人三星"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("""{"suggestion":"a","reason":"b","confidence":1.5}""")]
    [InlineData("""{"suggestion":"a","reason":"b","confidence":-0.1}""")]
    public async Task AdviseAsync_ConfidenceOutOfRange_ThrowsInvalidOperationException(string json)
    {
        var fake = new FakeAdvisorVlmClient().QueueResult(json);
        var advisor = new LLMAdvisor(fake);

        var act = async () => await advisor.AdviseAsync(new AdvisorEvent(AdvisorEventType.MajorTransition, "重大转阵"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AdviseAsync_RecordsPromptContainingEventType()
    {
        var fake = new FakeAdvisorVlmClient().QueueResult("""{"suggestion":"a","reason":"b","confidence":0.5}""");
        var advisor = new LLMAdvisor(fake);

        await advisor.AdviseAsync(new AdvisorEvent(AdvisorEventType.HexAugment, "海克斯三选一"));

        fake.CallCount.Should().Be(1);
        fake.LastPrompt.Should().NotBeNull();
        fake.LastPrompt.Should().Contain("HexAugment");
    }

    [Fact]
    public async Task AdviseAsync_ExceedingBudget_ThrowsBudgetExceeded()
    {
        var fake = new FakeAdvisorVlmClient();
        for (var i = 0; i < LLMAdvisor.Budget.MaxCalls; i++)
            fake.QueueResult("""{"suggestion":"a","reason":"b","confidence":0.5}""");

        var advisor = new LLMAdvisor(fake);

        for (var i = 0; i < LLMAdvisor.Budget.MaxCalls; i++)
            await advisor.AdviseAsync(new AdvisorEvent(AdvisorEventType.HexAugment, "海克斯三选一"));

        var act = async () => await advisor.AdviseAsync(new AdvisorEvent(AdvisorEventType.HexAugment, "海克斯三选一"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("advisor budget exceeded");
    }

    [Fact]
    public async Task AdviseAsync_ConcurrentCalls_DoNotOvershootBudget()
    {
        var advisor = new LLMAdvisor(new FaultlessAdvisorVlmClient());

        var tasks = Enumerable.Range(0, LLMAdvisor.Budget.MaxCalls)
            .Select(_ => advisor.AdviseAsync(new AdvisorEvent(AdvisorEventType.HexAugment, "并发触发")));

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r != null);

        var act = async () => await advisor.AdviseAsync(new AdvisorEvent(AdvisorEventType.HexAugment, "并发触发"));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("advisor budget exceeded");
    }

    private sealed class FaultlessAdvisorVlmClient : IVlmClient
    {
        public Task<string> CompleteAsync(
            string prompt,
            IReadOnlyList<(string mime, byte[] data)> images,
            CancellationToken ct) =>
            Task.FromResult("""{"suggestion":"a","reason":"b","confidence":0.5}""");
    }
}