namespace ChanSight.Tests.Vision;

using ChanSight.Vision.Models;
using FluentAssertions;

public sealed class GameSeasonDictionaryTests
{
    [Fact]
    public void TryFuzzyMatch_Exact_ReturnsSelf()
    {
        var hero = GameSeasonDictionary.Heroes.First();

        GameSeasonDictionary.TryFuzzyMatch(hero, GameSeasonDictionary.Heroes).Should().Be(hero);
    }

    [Fact]
    public void TryFuzzyMatch_OneInsertion_Matches()
    {
        var hero = GameSeasonDictionary.Heroes.First();

        var result = GameSeasonDictionary.TryFuzzyMatch(hero + "x", GameSeasonDictionary.Heroes, 1);

        result.Should().Be(hero);
    }

    [Fact]
    public void TryFuzzyMatch_BeyondThreshold_ReturnsNull()
    {
        var hero = GameSeasonDictionary.Heroes.First();

        GameSeasonDictionary.TryFuzzyMatch(hero + "xy", GameSeasonDictionary.Heroes, 1).Should().BeNull();
    }

    [Fact]
    public void TryFuzzyMatch_Substitution_Matches()
    {
        var hero = GameSeasonDictionary.Heroes.First();
        var mutated = hero[..^1] + "x";

        var result = GameSeasonDictionary.TryFuzzyMatch(mutated, GameSeasonDictionary.Heroes, 1);

        result.Should().Be(hero);
    }

    [Fact]
    public void TryFuzzyMatch_TieBetweenCandidates_IsDeterministicMember()
    {
        var hero = GameSeasonDictionary.Heroes.First();
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { hero + "x", hero + "y" };

        var result = GameSeasonDictionary.TryFuzzyMatch(hero, candidates, 1);

        candidates.Should().Contain(result);
    }

    [Fact]
    public void TryFuzzyMatch_EmptyInput_ReturnsNull()
    {
        GameSeasonDictionary.TryFuzzyMatch(string.Empty, GameSeasonDictionary.Heroes, 2).Should().BeNull();
    }

    [Fact]
    public void TryFuzzyMatch_Items_WithNoise_Matches()
    {
        var item = GameSeasonDictionary.Items.First();
        var result = GameSeasonDictionary.TryFuzzyMatch(item + "'", GameSeasonDictionary.Items, 1);

        result.Should().Be(item);
    }

    [Fact]
    public void TryFuzzyMatch_Unknown_ReturnsNull()
    {
        GameSeasonDictionary.TryFuzzyMatch("zzzzzz", GameSeasonDictionary.Heroes, 2).Should().BeNull();
    }

    [Fact]
    public void TryFuzzyMatch_ZeroDistance_OnlyExact()
    {
        var hero = GameSeasonDictionary.Heroes.First();
        GameSeasonDictionary.TryFuzzyMatch(hero, GameSeasonDictionary.Heroes, 0).Should().Be(hero);
    }

    [Fact]
    public void ComputeLevenshtein_KnownPairs()
    {
        GameSeasonDictionary.ComputeLevenshteinDistance("abc", "abd").Should().Be(1);
        GameSeasonDictionary.ComputeLevenshteinDistance("", "ab").Should().Be(2);
        GameSeasonDictionary.ComputeLevenshteinDistance("kitten", "sitting").Should().Be(3);
        GameSeasonDictionary.ComputeLevenshteinDistance("same", "same").Should().Be(0);
    }

    [Fact]
    public void TryParseNumeric_Basic()
    {
        GameSeasonDictionary.TryParseNumeric("35").Should().Be(35);
        GameSeasonDictionary.TryParseNumeric("9").Should().Be(9);
        GameSeasonDictionary.TryParseNumeric("0").Should().Be(0);
        GameSeasonDictionary.TryParseNumeric("").Should().BeNull();
        GameSeasonDictionary.TryParseNumeric("abc").Should().BeNull();
        GameSeasonDictionary.TryParseNumeric("3.5").Should().Be(35);
    }

    [Fact]
    public void Heroes_Set_IsPopulated()
    {
        GameSeasonDictionary.Heroes.Should().NotBeEmpty();
        GameSeasonDictionary.Traits.Should().NotBeEmpty();
        GameSeasonDictionary.Items.Should().NotBeEmpty();
    }
}