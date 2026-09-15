namespace ChanSight.Core.Engine;

using ChanSight.Core.Data;
using System.Globalization;

public sealed record TacticalAdvisorOptions(
    int InterestStep = 10,
    int MaxInterest = 5,
    int LevelUpGoldThreshold = 50,
    int TargetMaxLevel = 9,
    double RollThreshold = 0.05,
    double StopThreshold = 0.01,
    int MinRollGold = 10);

public sealed class TacticalAdvisor : ITacticalAdvisor
{
    private const int MaxCompCount = 3;
    private const int ThreeStarCopies = 9;

    private const int PriorityLevelUp = 80;
    private const int PriorityHold = 60;
    private const int PriorityRoll = 90;
    private const int PrioritySmallRoll = 70;
    private const int PriorityLock = 75;
    private const int PriorityStop = 40;
    private const int PriorityComp = 50;

    private static readonly Dictionary<string, int> DefaultHeroCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["波比"] = 1, ["吉格斯"] = 1, ["艾希"] = 1, ["墨菲特"] = 1, ["弗拉基米尔"] = 1,
        ["盖伦"] = 1, ["崔丝塔娜"] = 1, ["辛德拉"] = 1, ["凯南"] = 1,
        ["布隆"] = 2, ["蔚"] = 2, ["卡蜜尔"] = 2, ["瑟庄妮"] = 2, ["慎"] = 2,
        ["图奇"] = 2, ["婕拉"] = 2, ["卡特琳娜"] = 2, ["璐璐"] = 2, ["露露"] = 2,
        ["菲奥娜"] = 2, ["瑟提"] = 2,
        ["阿狸"] = 3, ["德莱文"] = 3, ["莫甘娜"] = 3, ["厄斐琉斯"] = 3, ["希瓦娜"] = 3,
        ["俄洛伊"] = 3, ["佐伊"] = 3, ["维迦"] = 3, ["乐芙兰"] = 3, ["嘉文四世"] = 3,
        ["蕾欧娜"] = 3, ["安妮"] = 3,
        ["凯尔"] = 4, ["亚索"] = 4, ["崔斯特"] = 4, ["千珏"] = 4, ["拉克丝"] = 4,
        ["卡兹克"] = 4, ["斯维因"] = 4,
        ["永恩"] = 5, ["劫"] = 5, ["奥瑞利安·索尔"] = 5,
    };

    private readonly IHypergeometricEngine _engine;
    private readonly CompKnowledgeBase _knowledgeBase;
    private readonly TacticalAdvisorOptions _options;

    public TacticalAdvisor(
        IHypergeometricEngine engine,
        CompKnowledgeBase knowledgeBase,
        TacticalAdvisorOptions? options = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _knowledgeBase = knowledgeBase ?? throw new ArgumentNullException(nameof(knowledgeBase));
        _options = options ?? new TacticalAdvisorOptions();
    }

    public IReadOnlyList<TacticalRecommendation> Evaluate(GameStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var results = new List<TacticalRecommendation>();
        results.AddRange(EvaluateComp(state));
        results.AddRange(EvaluateEconomy(state));
        results.AddRange(EvaluateRoll(state));
        return results;
    }

    private IEnumerable<TacticalRecommendation> EvaluateComp(GameStateSnapshot state)
    {
        var ownedNames = CollectOwnedNames(state);
        if (ownedNames.Count == 0)
        {
            yield break;
        }

        var ownedSet = new HashSet<string>(ownedNames, StringComparer.OrdinalIgnoreCase);
        var templates = _knowledgeBase.QueryByUnits(ownedNames);

        var index = 0;
        foreach (var template in templates)
        {
            if (index >= MaxCompCount)
            {
                yield break;
            }

            var hits = CountHits(template, ownedSet);
            var missing = template.CoreUnits.Where(u => !ownedSet.Contains(u)).ToList();
            var risk = template.CoreUnits.Count == 0 ? 0.0 : Clamp((double)missing.Count / template.CoreUnits.Count);

            var evidence = new List<string>
            {
                $"命中核心/副核单位 {hits} 个",
                $"模板: {template.Name} ({template.Id})",
            };
            if (missing.Count > 0)
            {
                evidence.Add($"缺失核心: {string.Join("、", missing)}");
            }

            yield return new TacticalRecommendation(
                AdviceKind.CompRecommend,
                Verdict.Adopt,
                PriorityComp - index,
                $"命中 {hits} 个单位, 可朝「{template.Name}」阵容发展",
                risk,
                evidence);

            index++;
        }
    }

    private IEnumerable<TacticalRecommendation> EvaluateEconomy(GameStateSnapshot state)
    {
        if (state.Phase != GamePhase.Planning)
        {
            yield break;
        }

        var gold = Math.Max(0, state.Gold);
        var tier = Math.Min(_options.MaxInterest, gold / _options.InterestStep);
        var canLevelUp = state.Level < _options.TargetMaxLevel;

        if (gold >= _options.LevelUpGoldThreshold && canLevelUp)
        {
            yield return new TacticalRecommendation(
                AdviceKind.EconomyDecision,
                Verdict.LevelUp,
                PriorityLevelUp,
                $"金币充裕({gold} ≥ {_options.LevelUpGoldThreshold})且未满级, 建议升人口",
                0.35,
                new List<string>
                {
                    $"当前利息档 {tier}/{_options.MaxInterest}",
                    $"等级 {state.Level} < 目标等级 {_options.TargetMaxLevel}",
                });
            yield break;
        }

        var reason = gold < _options.InterestStep && canLevelUp
            ? $"金币 {gold} 不足单档利息({_options.InterestStep}), 建议持息等待"
            : $"未达升人口条件(金币 {gold} < {_options.LevelUpGoldThreshold}), 保持持息节奏";

        var nextTierTarget = Math.Min(_options.MaxInterest, tier + 1) * _options.InterestStep;
        yield return new TacticalRecommendation(
            AdviceKind.EconomyDecision,
            Verdict.Hold,
            PriorityHold,
            reason,
            0.1,
            new List<string>
            {
                $"当前利息档 {tier}/{_options.MaxInterest}",
                $"下一档目标 {nextTierTarget} 金币",
            });
    }

    private IEnumerable<TacticalRecommendation> EvaluateRoll(GameStateSnapshot state)
    {
        if (state.Phase != GamePhase.Planning)
        {
            yield break;
        }

        var candidates = BuildCandidates(state);
        if (candidates.Count == 0)
        {
            yield break;
        }

        if (state.ShopCards.Count > 0)
        {
            var lockCard = state.ShopCards
                .FirstOrDefault(c => !string.IsNullOrEmpty(c.Name) && candidates.ContainsKey(c.Name));
            if (lockCard is not null)
            {
                yield return new TacticalRecommendation(
                    AdviceKind.RollDecision,
                    Verdict.Lock,
                    PriorityLock,
                    $"商店出现待升星同名英雄「{lockCard.Name}」, 建议锁牌购买",
                    0.2,
                    new List<string> { $"商店卡 「{lockCard.Name}」 可直接购买" });
            }

            yield break;
        }

        foreach (var entry in candidates)
        {
            var name = entry.Key;
            var cost = entry.Value.Cost;
            var owned = entry.Value.Owned;
            var takenByOthers = CountOpponentCopies(state, name);
            var neededToThreeStar = Math.Max(0, ThreeStarCopies - owned);

            // The engine models a single 5-slot refresh (needed is capped at 5), so the roll
            // signal is the probability of drawing >=1 copy next refresh; the copies still
            // needed for 3-star are surfaced separately in the evidence for traceability.
            var probability = _engine.ProbabilityAtLeast(state.Level, cost, owned, takenByOthers, needed: 1);

            var (verdict, risk) = DecideRollVerdict(state.Gold, probability);

            var reason = verdict switch
            {
                Verdict.Roll => $"「{name}」刷新概率较高, 值得硬 D 冲 3 星",
                Verdict.SmallRoll => $"「{name}」刷新概率中等, 建议小 D 试探",
                Verdict.Stop => $"「{name}」刷新概率过低, 建议停手",
                _ => $"「{name}」建议持观望",
            };

            var evidence = new List<string>
            {
                $"英雄: {name} (费用 {cost}, 等级 {state.Level})",
                $"prob={probability.ToString("0.0000", CultureInfo.InvariantCulture)}",
                $"下次刷新至少再得一张概率约 {FormatPercent(probability)}",
                $"已持有 {owned} 张(还需 {neededToThreeStar} 张升 3 星), 对手持有 {takenByOthers} 张",
            };

            yield return new TacticalRecommendation(
                AdviceKind.RollDecision,
                verdict,
                verdict switch
                {
                    Verdict.Roll => PriorityRoll,
                    Verdict.SmallRoll => PrioritySmallRoll,
                    Verdict.Stop => PriorityStop,
                    _ => PriorityStop,
                },
                reason,
                risk,
                evidence);
        }
    }

    private (Verdict Verdict, double Risk) DecideRollVerdict(int gold, double probability)
    {
        if (gold < _options.MinRollGold)
        {
            return (Verdict.Stop, 0.15);
        }

        if (probability >= _options.RollThreshold)
        {
            return (Verdict.Roll, Clamp(1.0 - probability));
        }

        if (probability <= _options.StopThreshold)
        {
            return (Verdict.Stop, 0.1);
        }

        return (Verdict.SmallRoll, Clamp(1.0 - probability));
    }

    private static List<string> CollectOwnedNames(GameStateSnapshot state)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in state.BoardUnits.Concat(state.BenchUnits))
        {
            if (!string.IsNullOrEmpty(unit.Name))
            {
                names.Add(unit.Name);
            }
        }

        return names.ToList();
    }

    private static Dictionary<string, (int Cost, int Owned)> BuildCandidates(GameStateSnapshot state)
    {
        var candidates = new Dictionary<string, (int Cost, int Owned)>(StringComparer.OrdinalIgnoreCase);
        var maxStars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var unit in state.BoardUnits.Concat(state.BenchUnits))
        {
            if (string.IsNullOrEmpty(unit.Name))
            {
                continue;
            }

            var name = unit.Name;
            maxStars.TryGetValue(name, out var currentMax);
            maxStars[name] = Math.Max(currentMax, unit.Star);

            candidates.TryGetValue(name, out var entry);
            candidates[name] = (CostOf(name), entry.Owned + StarCopies(unit.Star));
        }

        foreach (var name in maxStars.Where(kv => kv.Value > 2).Select(kv => kv.Key).ToList())
        {
            candidates.Remove(name);
        }

        return candidates;
    }

    private static int CountOpponentCopies(GameStateSnapshot state, string name)
    {
        var total = 0;
        foreach (var opponent in state.Opponents)
        {
            foreach (var unit in opponent.BoardUnits)
            {
                if (string.Equals(unit.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    total += StarCopies(unit.Star);
                }
            }
        }

        return total;
    }

    private static int CountHits(CompTemplate template, HashSet<string> owned)
    {
        var hits = 0;
        foreach (var unit in template.CoreUnits.Concat(template.SecondaryUnits))
        {
            if (owned.Contains(unit))
            {
                hits++;
            }
        }

        return hits;
    }

    private static int StarCopies(int star) => star switch
    {
        1 => 1,
        2 => 3,
        3 => 9,
        _ => 0,
    };

    private static int CostOf(string name) =>
        DefaultHeroCosts.TryGetValue(name, out var cost) ? cost : 1;

    private static string FormatPercent(double probability) =>
        (probability * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    private static double Clamp(double value)
    {
        if (double.IsNaN(value))
        {
            return 0.0;
        }

        if (value < 0.0)
        {
            return 0.0;
        }

        if (value > 1.0)
        {
            return 1.0;
        }

        return value;
    }
}