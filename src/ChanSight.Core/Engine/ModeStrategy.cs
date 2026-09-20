namespace ChanSight.Core.Engine;

/// <summary>
/// 按当前 season 的 mode 解析战术推荐参数(<see cref="TacticalAdvisorOptions"/>)。
/// 只影响算法参数, 不影响字典; 各模式默认值基于金铲铲玩法常识集中在此,
/// 调用方不硬编码魔法值。未知/空 mode 回退到默认参数组。
/// </summary>
public static class ModeStrategy
{
    /// <summary>
    /// 按 mode 返回一套战术参数。已知模式("恭喜发财"/"匹配"/"狂暴")各自一组;
    /// 未知或空回退 <see cref="TacticalAdvisorOptions"/> 默认值。
    /// </summary>
    public static TacticalAdvisorOptions Resolve(string? mode) => (mode ?? string.Empty) switch
    {
        // 恭喜发财: 高经济、升人口最激进、追高费与强力强化, D 牌更果断。
        "恭喜发财" => new TacticalAdvisorOptions(
            InterestStep: 10,
            MaxInterest: 5,
            LevelUpGoldThreshold: 30,
            TargetMaxLevel: 10,
            RollThreshold: 0.03,
            StopThreshold: 0.005,
            MinRollGold: 5),

        // 狂暴: 节奏更快、经济更紧, 升人口与搜牌更果断, 早期定阵。
        "狂暴" => new TacticalAdvisorOptions(
            InterestStep: 10,
            MaxInterest: 5,
            LevelUpGoldThreshold: 40,
            TargetMaxLevel: 9,
            RollThreshold: 0.04,
            StopThreshold: 0.008,
            MinRollGold: 8),

        // 匹配: 标准排位/匹配节奏, 均衡运营。
        "匹配" => new TacticalAdvisorOptions(
            InterestStep: 10,
            MaxInterest: 5,
            LevelUpGoldThreshold: 50,
            TargetMaxLevel: 9,
            RollThreshold: 0.05,
            StopThreshold: 0.01,
            MinRollGold: 10),

        _ => new TacticalAdvisorOptions(),
    };
}
