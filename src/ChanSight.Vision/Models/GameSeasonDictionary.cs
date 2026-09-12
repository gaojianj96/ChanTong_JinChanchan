namespace ChanSight.Vision.Models;

public static class GameSeasonDictionary
{
    public static readonly IReadOnlySet<string> Heroes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "亚索", "阿狸", "辛德拉", "千珏", "艾希", "布隆", "拉克丝", "盖伦",
        "德莱文", "卡特琳娜", "凯南", "慎", "永恩", "劫", "伊泽瑞尔",
        "薇古丝", "金克丝", "蔚", "杰斯", "塔姆", "瑟提", "沃里克",
        "斯卡纳", "莫甘娜", "弗拉基米尔", "斯维因", "诺提勒斯", "崔斯特",
        "菲奥娜", "普朗克", "艾瑞莉娅", "雷克塞", "科加斯", "玛尔扎哈",
        "卡莎", "吉格斯", "波比", "佐伊", "莉莉娅", "赫卡里姆", "内瑟斯",
        "俄洛伊", "墨菲特", "蕾欧娜", "黛安娜", "塔里克", "索拉卡",
        "卡莉斯塔", "妮蔻", "瑟庄妮", "丽桑卓", "阿兹尔", "艾翁",
        "阿克尚", "格温", "佛耶戈", "赵信", "瑞兹", "奥拉夫", "赛娜",
        "卢锡安", "厄斐琉斯", "拉克丝", "嘉文四世", "希瓦娜",
        "奥瑞利安·索尔", "科加斯", "克格莫", "图奇", "布里茨",
        "卡蜜尔", "乐芙兰", "艾克", "纳尔", "雷克顿",
        "安妮", "黑默丁格", "璐璐", "露露", "维迦", "崔丝塔娜",
        "凯尔", "莫德凯撒", "千珏", "黛安娜", "婕拉", "茂凯",
        "孙悟空", "易", "李青", "薇恩", "亚托克斯", "卡兹克",
        "塞恩", "赫卡里姆", "维克托", "玛尔扎哈", "卡尔玛",
        "赛恩", "潘森", "雷恩加尔", "尼菈", "泽丽", "希维尔",
        "辛吉德", "厄加特", "蒙多", "迦娜"
    };

    public static readonly IReadOnlySet<string> Traits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "法师", "护卫", "狙神", "斗士", "刺客", "剑士", "骑士",
        "游侠", "换形师", "极地", "贵族", "浪人", "忍者", "虚空",
        "龙", "暗影", "机器人", "圣盾使", "源计划", "佣兵",
        "未来战士", "太空海盗", "星之守护者", "爆破专家", "战地机甲",
        "征服者", "复苏者", "黎明使者", "黑夜使者", "破败军团", "小恶魔",
        "约德尔人", "社交名流", "狙神", "执法官", "巨像", "发明家",
        "赏金猎人", "海克斯科技", "炼金科技", "黑魔法师", "白魔法师",
        "枪手", "刺客", "战斗学院", "帝国", "辛迪加", "挑战者",
        "星界龙", "怒翼龙", "玉龙", "金鳞龙", "幻境龙", "风暴龙", "幽影龙",
        "神龙尊者", "冒险者", "重骑兵", "屠龙勇士", "格斗家", "炮手",
        "超英", "怪兽", "源计划", "地下魔盗团", "平行宇宙",
        "心之钢", "K/DA", "真实伤害", "EMO", "五杀摇滚", "迪斯科",
        "8比特", "朋克", "大腕明星", "乡村音乐"
    };

    public static readonly IReadOnlySet<string> Items = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "无尽之刃", "正义之手", "蓝霸符", "反曲之弓", "无用大棒",
        "女神之泪", "锁子甲", "负极斗篷", "巨人腰带", "暴风大剑",
        "日炎斗篷", "狂徒铠甲", "巨龙之爪", "棘刺背心", "离子火花",
        "救赎", "石像鬼石板甲", "泰坦的坚决", "水银", "鬼索的狂暴之刃",
        "卢安娜的飓风", "最后的轻语", "巨人杀手", "饮血剑",
        "海克斯科技枪刃", "死亡之刃", "灭世者的死亡之帽", "珠光护手",
        "朔极之矛", "大天使之杖", "斯塔缇克电刃", "莫雷洛秘典",
        "窃贼手套", "灵风", "静止法衣", "基克的先驱",
        "钢铁烈阳之匣", "能量圣杯", "兹若特传送门",
        "奥恩装备", "金铲铲", "金铲铲冠冕"
    };

    public static readonly IReadOnlySet<string> Numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"
    };

    public static int ComputeLevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        int lenA = a.Length;
        int lenB = b.Length;

        if (lenA == 0) return lenB;
        if (lenB == 0) return lenA;

        Span<int> prevRow = stackalloc int[lenB + 1];
        Span<int> currRow = stackalloc int[lenB + 1];

        for (int j = 0; j <= lenB; j++)
            prevRow[j] = j;

        for (int i = 1; i <= lenA; i++)
        {
            currRow[0] = i;
            for (int j = 1; j <= lenB; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                int deletion = prevRow[j] + 1;
                int insertion = currRow[j - 1] + 1;
                int substitution = prevRow[j - 1] + cost;

                currRow[j] = Math.Min(deletion, Math.Min(insertion, substitution));
            }

            Span<int> temp = prevRow;
            prevRow = currRow;
            currRow = temp;
        }

        return prevRow[lenB];
    }

    public static string? TryFuzzyMatch(string text, IReadOnlySet<string> dictionary, int maxDistance = 2)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.AsSpan().Trim();

        var trimmedString = trimmed.ToString();
        if (dictionary.Contains(trimmedString))
            return trimmedString;

        string? bestMatch = null;
        int bestDistance = int.MaxValue;

        foreach (var entry in dictionary)
        {
            int distance = ComputeLevenshteinDistance(trimmed, entry.AsSpan());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestMatch = entry;
            }

            if (bestDistance == 0)
                break;
        }

        return bestDistance <= maxDistance ? bestMatch : null;
    }

    public static int? TryParseNumeric(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '-').ToArray());

        if (int.TryParse(cleaned, out int result))
            return result;

        return null;
    }
}