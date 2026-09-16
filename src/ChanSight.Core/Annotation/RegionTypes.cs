namespace ChanSight.Core.Annotation;

/// <summary>
/// <see cref="CorrectionRecord.RegionType"/> / <see cref="GoldLabel.RegionType"/> 的字段级字符串枚举约定。
/// </summary>
public static class RegionTypes
{
    public const string BoardHero = "board.hero";

    public const string BoardStar = "board.star";

    public const string BoardItems = "board.items";

    public const string BenchHero = "bench.hero";

    public const string ShopHero = "shop.hero";

    public const string Gold = "gold";

    public const string Level = "level";

    public const string Stage = "stage";

    public const string Hp = "hp";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        BoardHero,
        BoardStar,
        BoardItems,
        BenchHero,
        ShopHero,
        Gold,
        Level,
        Stage,
        Hp
    };

    public static bool IsValid(string? regionType) => regionType is not null && All.Contains(regionType);
}