using ChanSight.Core.MetaInfo;
using FluentAssertions;
using System.Text.Json;

namespace ChanSight.Tests.MetaInfo;

public sealed class MetaInfoModelTests
{
    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, MetaJson.Options);
        return JsonSerializer.Deserialize<T>(json, MetaJson.Options)!;
    }

    [Fact]
    public void MetaSource_RoundTripsAllFields()
    {
        var source = new MetaSource
        {
            Type = MetaSourceType.ThirdParty,
            Provider = "op.gg",
            Patch = "14.18",
            Url = "https://example.com/comp",
            Confidence = 0.87,
        };

        RoundTrip(source).Should().BeEquivalentTo(source);
    }

    [Fact]
    public void IntrinsicRequirements_RoundTripsAllFields()
    {
        var requirements = new IntrinsicRequirements
        {
            RequiredTwoStarUnits = [new UnitCountRequirement { UnitName = "Caitlyn", MinCount = 2 }],
            RequiredEmblems = ["Sorcerer"],
            RequiredItems = ["Blue Buff"],
            RequiredAugments = ["Pandora's Items"],
            MinUnitCounts = [new UnitCountRequirement { UnitName = "Jarvan IV", MinCount = 1 }],
        };

        RoundTrip(requirements).Should().BeEquivalentTo(requirements);
    }

    [Fact]
    public void SituationalThresholds_RoundTripsAllFields()
    {
        var thresholds = new SituationalThresholds
        {
            MinGold = 50,
            MinHp = 20,
            MinLevel = 8,
            ByStage = "4-1",
            MaxContestedPlayers = 2,
            BoardConflictNote = "avoid if contested",
        };

        RoundTrip(thresholds).Should().BeEquivalentTo(thresholds);
    }

    [Fact]
    public void BoardLayout_RoundTripsPlacementsAndNote()
    {
        var layout = new BoardLayout
        {
            Placements =
            [
                new BoardPlacement("Caitlyn", 0, 0),
                new BoardPlacement("Vi", 3, 6),
                new BoardPlacement("Jarvan IV", 2, 3),
            ],
            PositioningNote = "frontline on row 0",
        };

        RoundTrip(layout).Should().BeEquivalentTo(layout);
    }

    [Fact]
    public void CompMeta_RoundTripsAllFields()
    {
        var meta = new CompMeta
        {
            Id = "sniper-brawler",
            Name = "Sniper Brawler",
            CompCode = "SN-BR",
            IntrinsicRequirements = new IntrinsicRequirements
            {
                RequiredTwoStarUnits = [new UnitCountRequirement { UnitName = "Caitlyn", MinCount = 2 }],
                RequiredEmblems = ["Bruiser"],
                RequiredItems = ["Infinity Edge"],
                RequiredAugments = ["Sniper's Nest"],
                MinUnitCounts = [new UnitCountRequirement { UnitName = "Vi", MinCount = 1 }],
            },
            Situational = new SituationalThresholds { MinGold = 50, ByStage = "4-1" },
            Units = [new UnitBuild { HeroName = "Caitlyn", Cost = 4, Items = ["Infinity Edge", "Last Whisper"], Note = "carry" }],
            Leveling = [new StageStep { Stage = "3-2", Action = "level to 6" }],
            Variants = [new Variant { Condition = "no brawler emblem", Plan = "play 4 snipers" }],
            SettleNote = "stable at 8",
            Augments = [new Augment { Name = "Sniper's Nest", Stage = "2-1", Effect = "bonus attack speed" }],
            Traits = [new TraitThreshold { Trait = "Sniper", Thresholds = [2, 4] }],
            BoardLayout = new BoardLayout { Placements = [new BoardPlacement("Caitlyn", 0, 0)] },
            Transformations = ["Caitlyn -> Aphelios"],
            Source = new MetaSource { Type = MetaSourceType.UserImage, Provider = "lobby", Patch = "14.18", Confidence = 0.95 },
        };

        RoundTrip(meta).Should().BeEquivalentTo(meta);
    }

    [Fact]
    public void Tiering_RoundTripsAllFields()
    {
        var version = new VersionMeta
        {
            Patch = "14.18",
            EnvironmentNote = "b-patch",
            Playstyle = "fast 8",
            Tiers =
            [
                new TierEntry
                {
                    CompId = "sniper-brawler",
                    Tier = CompTier.T1,
                    Conditionals = [new ConditionalTierShift { Condition = "uncontested", ShiftedTier = CompTier.T0, Effect = "winning" }],
                    Note = "strong mid-game",
                    Source = new MetaSource { Type = MetaSourceType.ThirdParty, Provider = "meta.tft", Patch = "14.18", Confidence = 0.8 },
                },
            ],
            Source = new MetaSource { Type = MetaSourceType.Manual, Provider = "curator", Patch = "14.18", Confidence = 1.0 },
        };

        RoundTrip(version).Should().BeEquivalentTo(version);
    }

    [Fact]
    public void RelationAndTip_RoundTripAllFields()
    {
        var relation = new CompRelation
        {
            FromCompId = "sniper-brawler",
            ToCompId = "knight-noble",
            Kind = RelationKind.Counter,
            Condition = "frontline heavy",
            Source = new MetaSource { Type = MetaSourceType.Manual, Provider = "curator", Patch = "14.18", Confidence = 0.7 },
        };

        var tip = new Tip
        {
            Id = "tip-1",
            CompId = "sniper-brawler",
            Content = "hold Caitlyn pair early",
            Confidence = 0.9,
            Evidence = ["match-1", "match-2"],
            Source = new MetaSource { Type = MetaSourceType.UserImage, Provider = "lobby", Patch = "14.18", Confidence = 0.9 },
        };

        RoundTrip(relation).Should().BeEquivalentTo(relation);
        RoundTrip(tip).Should().BeEquivalentTo(tip);
    }

    [Fact]
    public void PlayerDeclaration_RoundTripsConditions()
    {
        var declaration = new PlayerDeclaration
        {
            Conditions = new Dictionary<string, ConditionStatus>
            {
                ["sniper-brawler"] = ConditionStatus.Has,
                ["knight-noble"] = ConditionStatus.Missing,
                ["assassin-ninja"] = ConditionStatus.Unknown,
            },
        };

        RoundTrip(declaration).Should().BeEquivalentTo(declaration);
    }

    [Fact]
    public void Enums_SerializeAsStrings()
    {
        JsonSerializer.Serialize(CompTier.T0, MetaJson.Options).Should().Be("\"T0\"");
        JsonSerializer.Serialize(CompTier.T1, MetaJson.Options).Should().Be("\"T1\"");
        JsonSerializer.Serialize(CompTier.T2, MetaJson.Options).Should().Be("\"T2\"");
        JsonSerializer.Serialize(MetaSourceType.UserImage, MetaJson.Options).Should().Be("\"UserImage\"");
        JsonSerializer.Serialize(MetaSourceType.Manual, MetaJson.Options).Should().Be("\"Manual\"");
        JsonSerializer.Serialize(MetaSourceType.ThirdParty, MetaJson.Options).Should().Be("\"ThirdParty\"");
        JsonSerializer.Serialize(RelationKind.Flex, MetaJson.Options).Should().Be("\"Flex\"");
        JsonSerializer.Serialize(RelationKind.Upgrade, MetaJson.Options).Should().Be("\"Upgrade\"");
        JsonSerializer.Serialize(RelationKind.Counter, MetaJson.Options).Should().Be("\"Counter\"");
        JsonSerializer.Serialize(ConditionStatus.Has, MetaJson.Options).Should().Be("\"Has\"");
        JsonSerializer.Serialize(ConditionStatus.Missing, MetaJson.Options).Should().Be("\"Missing\"");
        JsonSerializer.Serialize(ConditionStatus.Unknown, MetaJson.Options).Should().Be("\"Unknown\"");
    }

    [Fact]
    public void SerializedJson_UsesCamelCasePropertyNames()
    {
        var source = new MetaSource { Type = MetaSourceType.Manual, Provider = "curator", Patch = "14.18", Confidence = 1.0 };

        var json = JsonSerializer.Serialize(source, MetaJson.Options);

        json.Should().Contain("\"type\":\"Manual\"");
        json.Should().Contain("\"provider\":\"curator\"");
        json.Should().Contain("\"patch\":\"14.18\"");
        json.Should().NotContain("\"Provider\"");
    }

    [Fact]
    public void MissingFields_DeserializeWithDefaults_DoesNotThrow()
    {
        var unit = JsonSerializer.Deserialize<UnitBuild>("{}", MetaJson.Options);

        unit.Should().NotBeNull();
        unit!.HeroName.Should().BeEmpty();
        unit.Cost.Should().Be(0);
        unit.Items.Should().NotBeNull().And.BeEmpty();
        unit.Note.Should().BeNull();
    }

    [Fact]
    public void MissingFields_OnCompMeta_DoesNotThrow()
    {
        var act = () => JsonSerializer.Deserialize<CompMeta>("{}", MetaJson.Options);

        act.Should().NotThrow();

        var meta = act();
        meta!.Id.Should().BeEmpty();
        meta.Units.Should().NotBeNull().And.BeEmpty();
        meta.Situational.Should().BeNull();
        meta.BoardLayout.Should().BeNull();
    }

    [Fact]
    public void BoardPlacement_RejectsOutOfRangeRowOrCol()
    {
        var invalidRow = () => new BoardPlacement("Caitlyn", 4, 0);
        var invalidCol = () => new BoardPlacement("Caitlyn", 0, 7);
        var negativeRow = () => new BoardPlacement("Caitlyn", -1, 0);
        var negativeCol = () => new BoardPlacement("Caitlyn", 0, -1);

        invalidRow.Should().Throw<ArgumentOutOfRangeException>();
        invalidCol.Should().Throw<ArgumentOutOfRangeException>();
        negativeRow.Should().Throw<ArgumentOutOfRangeException>();
        negativeCol.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BoardPlacement_AcceptsBoundaryRowAndCol()
    {
        var placement = () => new BoardPlacement("Caitlyn", 3, 6);

        placement.Should().NotThrow();
        placement().Should().BeEquivalentTo(new BoardPlacement("Caitlyn", 3, 6));
    }

    [Fact]
    public void Collections_DefaultToEmptyOnMissingFields()
    {
        var unit = JsonSerializer.Deserialize<UnitBuild>("{\"heroName\":\"Caitlyn\"}", MetaJson.Options);
        var trait = JsonSerializer.Deserialize<TraitThreshold>("{\"trait\":\"Sniper\"}", MetaJson.Options);
        var layout = JsonSerializer.Deserialize<BoardLayout>("{}", MetaJson.Options);
        var declaration = JsonSerializer.Deserialize<PlayerDeclaration>("{}", MetaJson.Options);

        unit!.Items.Should().NotBeNull().And.BeEmpty();
        trait!.Thresholds.Should().NotBeNull().And.BeEmpty();
        layout!.Placements.Should().NotBeNull().And.BeEmpty();
        declaration!.Conditions.Should().NotBeNull().And.BeEmpty();
    }
}
