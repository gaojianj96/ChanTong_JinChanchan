namespace ChanSight.Core.Season;

/// <summary>
/// S18(自然之力)内置种子字典。
///
/// 数据来源(可追溯):
///   1. <c>docs/meta/comp_code_table.md</c> + <c>data/meta/S18/comps.json</c>(27 个 S18 阵容, 小鱼一图流),
///      其中阵容名/前置/升降档隐含的英雄名(轮子/韦鲁斯/螳螂/女警/月男/阿狸/沙皇/卡蜜尔/剑圣/小蓝/奥恩/凯南/婕拉/瑟提/蔚…)
///      与装备名(青龙刀/羊刀/战刃/大剑/帽子/天使/科技枪/法爆/轻语/神器…)为唯一事实来源;
///   2. 与 S16.5 种子(<see cref="SeasonDictionarySeed"/>)取并集: S18 与 S16.5 英雄大量重叠, 此处继承
///      S16.5 集合再补 S18 新增英雄与俗称别名, 保证阵容码表出现的全部候选均被覆盖。
///
/// 关键语义: 英雄名用中文, 与 <see cref="SeasonDictionarySeed"/> 现有命名一致; 对俗称别名
/// (轮子/螳螂/月男/女警/沙皇/剑圣/天使/妖姬/大嘴/蜘蛛)同时保留, 因 VLM 识别/校验对 S18
/// 使用俗称候选。装备以俗称短名(青龙刀/羊刀/战刃/大剑/帽子/天使/科技枪/法爆/轻语)覆盖。
/// </summary>
public static class S18DictionarySeed
{
    public const string SeasonId = "S18";

    public static readonly IReadOnlySet<string> Heroes = CreateHeroes();

    public static readonly IReadOnlySet<string> Items = CreateItems();

    public static readonly IReadOnlySet<string> Traits = CreateTraits();

    public static readonly SeasonDictionary Seed = new(SeasonId, Heroes, Items, Traits);

    private static HashSet<string> CreateHeroes()
    {
        var heroes = new HashSet<string>(SeasonDictionarySeed.Heroes, StringComparer.OrdinalIgnoreCase)
        {
            // S18 新增英雄(官方名)。
            "奥恩", "韦鲁斯", "凯特琳", "伊莉丝", "提莫", "拉露恩",
            // S18 阵容码表用俗称别名(官方名见括号)。
            "轮子",   // 希维尔
            "小蓝",   // 官方身份待确认
            "蜘蛛",   // 伊莉丝
            "大嘴",   // 克格莫
            "女警",   // 凯特琳
            "螳螂",   // 卡兹克
            "月男",   // 厄斐琉斯
            "沙皇",   // 阿兹尔
            "剑圣",   // 易
            "天使",   // 凯尔
            "妖姬",   // 乐芙兰
        };

        return heroes;
    }

    private static HashSet<string> CreateItems()
    {
        var items = new HashSet<string>(SeasonDictionarySeed.Items, StringComparer.OrdinalIgnoreCase)
        {
            // S18 阵容码表用俗称装备短名(全称见括号)。
            "青龙刀", // 朔极之矛
            "羊刀",   // 鬼索的狂暴之刃
            "战刃",   // 无尽战刃(俗称)
            "大剑",   // 暴风大剑
            "帽子",   // 灭世者的死亡之帽
            "天使",   // 守护天使(俗称)
            "科技枪", // 海克斯科技枪刃
            "法爆",   // 珠光护手
            "轻语",   // 最后的轻语
            "神器",   // 神器(通用, 如卡蜜尔阵容 requiredItems)
        };

        return items;
    }

    private static HashSet<string> CreateTraits()
    {
        var traits = new HashSet<string>(SeasonDictionarySeed.Traits, StringComparer.OrdinalIgnoreCase)
        {
            // S18 阵容码表出现的羁绊(来自阵容名/转职/升档条件)。
            "地狱火", "迅射", "森林", "主宰", "神谕", "裁决",
            "丽花", "莲华", "野兽", "野", "猎人", "重装",
        };

        return traits;
    }
}
