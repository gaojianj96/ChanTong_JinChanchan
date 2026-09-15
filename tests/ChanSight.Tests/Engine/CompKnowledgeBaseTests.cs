namespace ChanSight.Tests.Engine;

using ChanSight.Core.Data;
using FluentAssertions;

public sealed class CompKnowledgeBaseTests
{
    private static string ValidDirectory => Path.Combine(
        AppContext.BaseDirectory, "Engine", "CompTemplates", "valid");

    private static string InvalidDirectory => Path.Combine(
        AppContext.BaseDirectory, "Engine", "CompTemplates", "invalid");

    [Fact]
    public void Load_LoadsAllTemplatesInDirectory()
    {
        var kb = new CompKnowledgeBase(ValidDirectory);

        kb.Templates.Should().HaveCount(3);
        kb.Templates.Select(t => t.Id).Should().BeEquivalentTo(
            "test-mage", "test-sniper", "test-assassin");
    }

    [Fact]
    public void QueryByUnits_ReturnsMatchesOrderedByHitCountDescending()
    {
        var kb = new CompKnowledgeBase(ValidDirectory);

        var owned = new[] { "拉克丝", "佐伊", "璐璐", "艾希" };

        var results = kb.QueryByUnits(owned);

        results.Should().HaveCount(2);
        results[0].Id.Should().Be("test-mage");   // 3 命中
        results[1].Id.Should().Be("test-sniper"); // 1 命中
    }

    [Fact]
    public void QueryByUnits_NoHits_ReturnsEmpty()
    {
        var kb = new CompKnowledgeBase(ValidDirectory);

        var results = kb.QueryByUnits(new[] { "亚索", "阿狸" });

        results.Should().BeEmpty();
    }

    [Fact]
    public void Load_InvalidJson_Throws()
    {
        var act = () => new CompKnowledgeBase(InvalidDirectory);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid JSON*");
    }

    [Fact]
    public void Load_DuplicateId_Throws()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Engine", "CompTemplates", "dup");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.json"),
            """{"id":"dup-id","name":"A","keyTraits":["法师"],"coreUnits":["拉克丝"],"secondaryUnits":[],"description":"x"}""");
        File.WriteAllText(Path.Combine(dir, "b.json"),
            """{"id":"dup-id","name":"B","keyTraits":["法师"],"coreUnits":["佐伊"],"secondaryUnits":[],"description":"x"}""");

        try
        {
            var act = () => new CompKnowledgeBase(dir);
            act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_UnknownHero_Throws()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Engine", "CompTemplates", "unknown-hero");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.json"),
            """{"id":"unknown-hero","name":"A","keyTraits":["法师"],"coreUnits":["不存在的英雄"],"secondaryUnits":[],"description":"x"}""");

        try
        {
            var act = () => new CompKnowledgeBase(dir);
            act.Should().Throw<InvalidOperationException>().WithMessage("*unknown hero*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_UnknownTrait_Throws()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Engine", "CompTemplates", "unknown-trait");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.json"),
            """{"id":"unknown-trait","name":"A","keyTraits":["不存在的羁绊"],"coreUnits":["拉克丝"],"secondaryUnits":[],"description":"x"}""");

        try
        {
            var act = () => new CompKnowledgeBase(dir);
            act.Should().Throw<InvalidOperationException>().WithMessage("*unknown key trait*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}