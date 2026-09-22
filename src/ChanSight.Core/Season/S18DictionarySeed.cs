namespace ChanSight.Core.Season;

/// <summary>
/// S18(自然之力)内置种子字典, 数据来自 topmeta.gg 权威数据(版本 18.2b):
/// 65 英雄(含费用)、35 羁绊、装备(主要成型装备 + 纹章, 不含"光明版"变体)。
/// 只存正式名, 不含俗称; 俗称→正式名的映射见 <see cref="HeroAliases"/>/<see cref="ItemAliases"/>。
/// </summary>
public static class S18DictionarySeed
{
    public const string SeasonId = "S18";

    public static readonly IReadOnlyDictionary<string, int> HeroCosts = CreateHeroCosts();

    public static readonly IReadOnlySet<string> Heroes = CreateHeroes();

    public static readonly IReadOnlySet<string> Items = CreateItems();

    public static readonly IReadOnlySet<string> Traits = CreateTraits();

    /// <summary>俗称英雄名 → 正式英雄名(供识别/回显映射)。</summary>
    public static readonly IReadOnlyDictionary<string, string> HeroAliases = CreateHeroAliases();

    /// <summary>俗称装备名 → 正式装备名(供识别/回显映射)。</summary>
    public static readonly IReadOnlyDictionary<string, string> ItemAliases = CreateItemAliases();

    public static readonly SeasonDictionary Seed = new(SeasonId, Heroes, Items, Traits)
    {
        HeroCosts = HeroCosts,
    };

    private static HashSet<string> CreateHeroes() =>
        new(HeroCosts.Keys, StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> CreateItems()
    {
        var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 主要成型装备(方案 a, 不含"光明版"变体)。
            "金锅锅冠冕", "智慧末刃", "金铲铲冠冕", "冕卫", "适应性头盔",
            "斯特拉克的挑战护手", "离子火花", "薄暮法袍", "棘刺背心", "窃贼手套",
            "坚定之心", "水银", "红霸符", "莫雷洛秘典", "夜之锋刃", "烁刃",
            "圣盾使的誓约", "强袭者的链枷", "永恒契约", "黎明核心", "虚空之杖",
            "饮血剑", "巨人杀手", "海克斯科技枪刃", "纳什之牙", "正义之手",
            "灭世者的死亡之帽", "巨龙之爪", "蓝霸符", "日炎斗篷", "泰坦的坚决",
            "死亡之刃", "大天使之杖", "最后的轻语", "金币收集者", "朔极之矛",
            "巫妖之祸", "海妖之怒", "狂徒铠甲", "密银黎明", "无尽之刃",
            "卢登的激荡", "飞升护符", "枯萎珠宝", "振奋盔甲", "恶火小斧",
            "探索者的护臂", "珠光护手", "中娅悖论", "虚空护手", "石像鬼石板甲",
            "顽强不屈", "连指手套", "疾射火炮", "视界专注", "三相之力",
            "魔蕴", "黎明圣盾", "巨型九头蛇", "斯塔缇克电刃", "黄昏圣盾",
            "鬼索的狂暴之刃", "鱼骨头",
            // 纹章类。
            "主宰纹章", "斗士纹章", "重装战士纹章", "绝命花妖纹章", "神谕纹章",
            "裁决使纹章", "永恒之森纹章", "迅捷射手纹章", "护卫纹章", "月蚀骑士纹章",
            "法师纹章", "花仙子纹章", "地狱火纹章", "约德尔人和朋友纹章", "野兽之灵纹章",
            "狂战士纹章", "猎人纹章",
        };

        return items;
    }

    private static HashSet<string> CreateTraits() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            "自然之力！大元素使", "月蚀骑士", "法师", "月华神女", "斗士",
            "永恒之森", "约德尔人和朋友", "重装战士", "宝石骑士", "翠神",
            "灵魂莲华", "猎人", "裁决使", "地狱火", "顶级掠食者",
            "峡谷野怪", "远古树精", "主宰", "赏金猎人", "魔战士",
            "野兽之灵", "绝命花妖", "迅捷射手", "帝王斑蝶", "神谕",
            "狂战士", "护卫", "花仙子", "召唤师", "荆棘之兴",
            "宿敌", "魔岩巨兽", "黑荆棘", "魔女", "日蚀骑士",
        };

    private static Dictionary<string, int> CreateHeroCosts() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            // 1 费。
            ["卡尔玛"] = 1,
            ["卡蜜尔"] = 1,
            ["可酷伯"] = 1,
            ["奥恩"] = 1,
            ["洛"] = 1,
            ["约里克"] = 1,
            ["维迦"] = 1,
            ["蕾欧娜"] = 1,
            ["阿卡丽"] = 1,
            ["雷克塞"] = 1,
            ["霞"] = 1,
            ["韦鲁斯"] = 1,
            ["绯红树怪"] = 1,
            ["苍蓝哨戒"] = 1,
            // 2 费。
            ["乐芙兰"] = 2,
            ["伊莉丝"] = 2,
            ["凯尔"] = 2,
            ["凯特琳"] = 2,
            ["慎"] = 2,
            ["提莫"] = 2,
            ["沃里克"] = 2,
            ["瑟庄妮"] = 2,
            ["芸阿娜"] = 2,
            ["阿利斯塔"] = 2,
            ["峡谷迅捷蟹"] = 2,
            ["暗影狼"] = 2,
            ["魔沼蛙"] = 2,
            // 3 费。
            ["克格莫"] = 3,
            ["卡兹克"] = 3,
            ["卡西奥佩娅"] = 3,
            ["崔丝塔娜"] = 3,
            ["拉莫斯"] = 3,
            ["易"] = 3,
            ["蔚"] = 3,
            ["费德提克"] = 3,
            ["赫卡里姆"] = 3,
            ["阿兹尔"] = 3,
            ["雷恩加尔"] = 3,
            ["黛安娜"] = 3,
            ["深红锋喙鸟"] = 3,
            ["远古石甲虫"] = 3,
            // 4 费。
            ["伊泽瑞尔"] = 4,
            ["厄斐琉斯"] = 4,
            ["墨菲特"] = 4,
            ["奈德丽"] = 4,
            ["婕拉"] = 4,
            ["希维尔"] = 4,
            ["瑟提"] = 4,
            ["索拉卡"] = 4,
            ["莉莉娅"] = 4,
            ["莫甘娜"] = 4,
            ["阿木木"] = 4,
            ["阿狸"] = 4,
            ["绯红印记树怪"] = 4,
            ["苍蓝雕纹魔像"] = 4,
            // 5 费。
            ["凯南"] = 5,
            ["塔里克"] = 5,
            ["德莱文"] = 5,
            ["拉克丝"] = 5,
            ["拉露恩"] = 5,
            ["纳尔"] = 5,
            ["艾希"] = 5,
            ["艾翁"] = 5,
            ["茂凯"] = 5,
            ["远古巨龙"] = 5,
        };

    private static Dictionary<string, string> CreateHeroAliases() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["轮子"] = "希维尔",
            ["大嘴"] = "克格莫",
            ["女警"] = "凯特琳",
            ["螳螂"] = "卡兹克",
            ["月男"] = "厄斐琉斯",
            ["沙皇"] = "阿兹尔",
            ["妖姬"] = "乐芙兰",
            ["剑圣"] = "易",
            ["蜘蛛"] = "伊莉丝",
            ["天使"] = "凯尔",
            ["猪妹"] = "瑟庄妮",
            ["小蓝"] = "苍蓝哨戒",
        };

    private static Dictionary<string, string> CreateItemAliases() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["羊刀"] = "鬼索的狂暴之刃",
            ["青龙刀"] = "朔极之矛",
            ["战刃"] = "锐利之刃",
            ["大剑"] = "暴风大剑",
            ["帽子"] = "灭世者的死亡之帽",
            ["法爆"] = "珠光护手",
            ["科技枪"] = "海克斯科技枪刃",
            ["轻语"] = "最后的轻语",
            ["大棒"] = "无用大棒",
            ["天使"] = "大天使之杖",
        };
}
