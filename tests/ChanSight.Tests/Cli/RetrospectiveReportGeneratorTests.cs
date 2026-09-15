using ChanSight.Cli.Retrospective;
using ChanSight.Recorder.Services;
using ChanSight.Tests.Vision;
using ChanSight.Vision.Interfaces;
using FluentAssertions;

namespace ChanSight.Tests.Cli;

public sealed class RetrospectiveReportGeneratorTests
{
    private const string ValidJson =
        """{"summary":"整体运营稳健","findings":[{"claim":"第 1 回合的 roll 决策风险偏高","evidenceSeqs":[1,2],"confidence":0.8}],"nextActions":["下局优先升人口"],"confidence":0.75}""";

    private static RetrospectiveReportGenerator Generator(IVlmClient client, int maxCalls = RetrospectiveReportGenerator.Budget.MaxCalls) =>
        new(client, new RetrospectiveScorer(), maxCalls);

    private static MatchEvent[] SampleEvents() => new[]
    {
        Event(1, MatchEventType.Decision, """{"decisionId":"d1","kind":"roll","verdict":"risky","reason":"low gold","risk":0.9}"""),
        Event(2, MatchEventType.Action, """{"decisionId":"d1"}"""),
        Event(3, MatchEventType.System, """{"outcome":"win"}""")
    };

    [Fact]
    public async Task GenerateAsync_ValidJson_ParsesReport()
    {
        var fake = new FakeAdvisorVlmClient().QueueResult(ValidJson);

        var report = await Generator(fake).GenerateAsync(SampleEvents());

        report.Summary.Should().NotBeNullOrWhiteSpace();
        report.Findings.Should().NotBeEmpty();
        report.Findings.Should().OnlyContain(f => f.EvidenceSeqs.Count > 0);
        report.Findings.Should().OnlyContain(f => f.Confidence >= 0.0 && f.Confidence <= 1.0);
        report.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task GenerateAsync_EmptyEvidenceSeqs_ThrowsFindingWithoutEvidence()
    {
        const string json = """{"summary":"s","findings":[{"claim":"无据结论","evidenceSeqs":[],"confidence":0.5}],"nextActions":[],"confidence":0.5}""";
        var fake = new FakeAdvisorVlmClient().QueueResult(json);

        var act = async () => await Generator(fake).GenerateAsync(SampleEvents());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("finding without evidence");
    }

    [Fact]
    public async Task GenerateAsync_EvidenceSeqNotInEvents_ThrowsFindingWithoutEvidence()
    {
        const string json = """{"summary":"s","findings":[{"claim":"引用不存在的事件","evidenceSeqs":[99],"confidence":0.5}],"nextActions":[],"confidence":0.5}""";
        var fake = new FakeAdvisorVlmClient().QueueResult(json);

        var act = async () => await Generator(fake).GenerateAsync(SampleEvents());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("finding without evidence");
    }

    [Fact]
    public async Task GenerateAsync_MalformedJson_ThrowsInvalidOperationException()
    {
        var fake = new FakeAdvisorVlmClient().QueueResult("{ not json");

        var act = async () => await Generator(fake).GenerateAsync(SampleEvents());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("""{"findings":[{"claim":"c","evidenceSeqs":[1],"confidence":0.5}],"nextActions":[],"confidence":0.5}""")]
    [InlineData("""{"summary":"s","findings":[{"claim":"c","evidenceSeqs":[1],"confidence":0.5}],"confidence":0.5}""")]
    [InlineData("""{"summary":"s","findings":[{"claim":"c","evidenceSeqs":[1],"confidence":0.5}],"nextActions":[]}""")]
    public async Task GenerateAsync_MissingField_ThrowsInvalidOperationException(string json)
    {
        var fake = new FakeAdvisorVlmClient().QueueResult(json);

        var act = async () => await Generator(fake).GenerateAsync(SampleEvents());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("""{"summary":"s","findings":[{"claim":"c","evidenceSeqs":[1],"confidence":0.5}],"nextActions":[],"confidence":1.5}""")]
    [InlineData("""{"summary":"s","findings":[{"claim":"c","evidenceSeqs":[1],"confidence":0.5}],"nextActions":[],"confidence":-0.1}""")]
    [InlineData("""{"summary":"s","findings":[{"claim":"c","evidenceSeqs":[1],"confidence":1.2}],"nextActions":[],"confidence":0.5}""")]
    public async Task GenerateAsync_ConfidenceOutOfRange_ThrowsInvalidOperationException(string json)
    {
        var fake = new FakeAdvisorVlmClient().QueueResult(json);

        var act = async () => await Generator(fake).GenerateAsync(SampleEvents());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateAsync_PromptContainsScoreAndEvidenceRequirements()
    {
        var fake = new FakeAdvisorVlmClient().QueueResult(ValidJson);

        await Generator(fake).GenerateAsync(SampleEvents());

        fake.LastPrompt.Should().NotBeNull();
        fake.LastPrompt.Should().Contain("Retrospective");
        fake.LastPrompt.Should().Contain("最终局面");
        fake.LastPrompt.Should().Contain("决策数");
        fake.LastPrompt.Should().Contain("evidenceSeqs");
        fake.LastPrompt.Should().Contain("Seq");
    }

    [Fact]
    public async Task GenerateAsync_ExceedingBudget_ThrowsBudgetExceeded()
    {
        const int maxCalls = 3;
        var fake = new FakeAdvisorVlmClient();
        for (var i = 0; i < maxCalls; i++)
            fake.QueueResult(ValidJson);

        var generator = Generator(fake, maxCalls);

        for (var i = 0; i < maxCalls; i++)
            await generator.GenerateAsync(SampleEvents());

        var act = async () => await generator.GenerateAsync(SampleEvents());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("report budget exceeded");
    }

    [Fact]
    public async Task GenerateAsync_EmptyEvents_StillInvokesClientWithoutNre()
    {
        const string json = """{"summary":"无事件对局","findings":[],"nextActions":[],"confidence":0.5}""";
        var fake = new FakeAdvisorVlmClient().QueueResult(json);

        var report = await Generator(fake).GenerateAsync(Array.Empty<MatchEvent>());

        fake.CallCount.Should().Be(1);
        report.Summary.Should().Be("无事件对局");
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_NullEvents_ThrowsArgumentNullException()
    {
        var act = async () => await Generator(new FakeAdvisorVlmClient()).GenerateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static MatchEvent Event(long seq, MatchEventType type, string payload) => new()
    {
        Seq = seq,
        Timestamp = DateTimeOffset.UtcNow,
        GameId = "g1",
        Type = type,
        PayloadJson = payload
    };
}