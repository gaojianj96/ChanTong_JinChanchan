# 阵容资讯系统 · 硬性条件剔除机制 审议 (Live)

> 日期: 2026-09-16 ｜ 模型: DeepSeek V4.1 Flash / MiniMax M3 / Qwen 3.8 Flash

## DeepSeek V4.1 Flash (需求)

①四字段不够。缺：海克斯门槛（指定强化/颜色/经济战力）、纹章可合成性与可替代、装备散件门槛（不是成装名）、等级/人口门槛、回合/血线门槛（如4-1前二星、70血可硬玩）、特定牌数量门槛（“蜘蛛多”“1-2螳螂”）、经济门槛（50块/连胜）。建议 CompRequirements 增：RequiredAugments、EmblemMode{必须/可替代}、RequiredComponents、MinLevel/MinHP/ByRound、MinUnitCounts、FallbackPlayable。每项标 hard/soft。

②三大识别缺口：先做V7不现实，装备/纹章/海克斯准确率不够，直接剔除会误杀。最贴合“直接剔除”的是：用户手动声明为主，V7识别为确认/补全。阵容卡展示前置条件，用户一键“有/无/未知”；未知不剔除，只标“待确认”。V7优先做海克斯名、纹章槽、成装三件高置信识别，识别到才自动满足，识别不到不判不满足。

③硬剔除只给不可替代且确定不满足：必需转职无、核心C指定成装无且散件不够、指定海克斯无、回合/血线门槛已过且关键二星未达、独家阵容核心同行≥阈值。保留标注：可硬玩、可替代装备、可转型、同行多但费池未枯竭、条件未知。剔除必须展示理由：缺什么、当前值、替代/硬玩建议。

④同行口径：MaxContestedPlayers 应是“核心同行”，不是同费池。核心同行=对手棋盘/备战命中本阵容核心牌集合且权重≥阈值。费池压力另用 takenByOthers 做概率扣减和排序，不直接硬剔除，除非核心牌被抢超阈值。0=独家专属只对核心牌完全不可被对手持有的阵容，且需用户确认。

⑤风险：过度剔除导致无阵容；信息错误误剔除。对策：兜底保留至少3套可硬玩/降级；剔除分层；未知不剔除；手动确认；可撤销；显示置信度；反馈纠错。条件库版本化，只标T0/T1。

⑥最大2风险：一，识别缺口导致误剔除，信任崩。对策：未知≠不满足，手动声明优先，V7只确认。二，条件库维护爆炸、标注不准导致过度剔除。对策：字段枚举+默认空，只对高优先阵容标硬条件，运营审核，用户可覆盖。

---

## MiniMax M3 (架构)

The user is asking me to review a design proposal for a "hard condition elimination" mechanism in a TFT (Teamfight Tactics) lineup recommendation system. They want me to be critical and provide specific solutions, not just agree. Let me carefully analyze each of the six points.

Let me think through each point carefully:

**① Four condition fields - are they sufficient? Missing hard conditions?**

The four fields are:
- MaxContestedPlayers (max same-line players)
- RequiredEmblems (needed emblem list)
- RequiredItems (core hero required items list)
- RequiredTwoStarUnits (must 2-star units)

What's missing?
- **经济门槛 (Economy threshold)**: Some comps need specific gold thresholds (e.g., fast 9 comps need 50+ gold, reroll comps need to stay at certain interest breakpoints). This is a hard condition - if you don't have enough gold, a fast 9 comp is infeasible.
- **血线门槛 (Health threshold)**: Some comps need time to scale (5-cost carries). If HP < 30, you can't play a slow scaling comp. This is genuinely a hard condition, not just a soft preference.
- **回合/阶段门槛 (Stage threshold)**: When is the comp "online"? If you're at 4-2 and the comp needs 8 population + 5-cost 2-star, it's simply too late.
- **人口门槛 (Population threshold)**: Some comps need specific level (e.g., 8人口 activated traits, 9人口 activated traits).
- **棋盘格子占用 (Board occupancy)**: If you already have 8 units on board, you can't fit a comp that requires specific synergies from those units.
- **已持有棋子冲突 (Existing unit conflicts)**: If you already have 4 Sentinels committed, a non-Sentinel comp is basically eliminated.
- **Key unit availability (核心棋子可用性)**: If the key carry is already taken by others (2+ players contesting), the comp might be eliminated regardless of MaxContestedPlayers.

So I think the four fields miss: economy, HP, stage, population, existing board commitment.

**② Recognition gaps for Hex/Emblems/Items - how to fill?**

Options:
- V7 recognition extension (add hex/emblem/item recognition)
- User manual declaration
- Hybrid

The user is asking which is most suitable for "direct elimination" usability.

For "direct elimination" to work reliably, you need HIGH CONFIDENCE in the condition data. If recognition is unreliable, hard elimination based on it will cause false negatives (wrongly eliminating good comps).

My recommendation: **Hybrid with priority on user declaration for hard conditions, recognition as supplement.**

Why? Because:
- V7 recognition for items is possible but OCR on TFT items is hard (small icons, similar shapes)
- Hex/augments recognition is also hard (3 choices, complex UI)
- User declaration is reliable but requires user effort
- The "hard elimination" decision has high cost (wrong elimination = no comp recommended)

Best approach: User declares the critical conditions (augments taken, key items available) in a "setup" step before recommendation. Recognition supplements but doesn't gate.

Actually, let me reconsider. The user said "当前游戏环境不满足→直接剔除". For this to work, we need to KNOW the current environment accurately. If we don't know what augments the user has, we can't eliminate emblems-dependent comps.

I think the answer is: **Hybrid - user declares augments (3 picks from recognition + confirmation), items auto-detected from board/bench where possible, emblems inferred from augments + user confirmation.**

For the most practical "direct elimination" usability:
- Augments: User confirms 3 picks (recognition suggests, user confirms) - this is the highest impact and lowest error cost
- Emblems: Inferred from augments + items on board
- Items: Recognition from board/bench units (easier than shop)

**③ Elimination vs annotation boundary**

Which should be hard-eliminated vs annotated?

Hard eliminate (binary, no recovery):
- MaxContestedPlayers exceeded (if 3+ people contesting, comp is dead)
- RequiredTwoStarUnits already impossible (if key unit is 5-cost and you're stage 4 with no copies)
- Population threshold not met for key trait activation
- HP too low for slow-scaling comp (e.g., < 20 HP and comp needs 2 more rounds to scale)

Annotate but keep (soft signal):
- RequiredItems partially met (some items, missing 1-2)
- RequiredEmblems not yet seen but augments could provide
- Two-star not yet achieved but achievable

The principle: **If the condition CANNOT be met in the remaining game time, eliminate. If it MIGHT be met, annotate.**

**④ Contested player counting - same core unit vs same cost pool**

This is a great question. Two interpretations:
- **Same core unit**: If 2+ players have the key carry (e.g., 3 players have Ahri 1-star), this comp is dead
- **Same cost pool**: If multiple players are contesting the same tier (e.g., 3 players rolling at level 7), pool is dry

These are different things:
- Core unit contesting = direct competition for specific units
- Cost pool contesting = indirect competition (everyone rolling at 7 means 4-cost pool is shared)

The current `takenByOthers` in E1 HypergeometricEngine - what does it track? It tracks units taken by others, which is closer to "same core unit" but at a per-unit level, not per-comp level.

For the MaxContestedPlayers field, I think the right interpretation is: **count players who hold the SAME KEY units (the comp's carry + key synergies)**. Not just same cost tier.

Connection to takenByOthers:
- takenByOthers gives per-unit counts (how many of unit X are taken)
- MaxContestedPlayers should aggregate: how many DISTINCT players hold at least one of the comp's key units
- This requires new logic: from OpponentSnapshot.BoardUnits, identify which opponents have which key units, count distinct players

So the gap: takenByOthers is unit-level, MaxContestedPlayers needs player-level aggregation. Need new logic.

**⑤ Risks of hard elimination**

1. **Over-elimination**: Too many conditions → no comps survive → system useless
   - Mitigation: Cap elimination to top-N candidates, always show at least 3-5 comps with "best available" annotation
   - Mitigation: Make conditions tiered (T0 absolute, T1 strong, T2 weak)

2. **Wrong condition data**: If user declares wrong augment or recognition mis-identifies → wrong comp eliminated
   - Mitigation: Show "why eliminated" with the specific condition that failed, allow user to override
   - Mitigation: Confidence scoring on condition data

3. **Combinatorial elimination**: Each condition eliminates some, but together eliminate all
   - Mitigation: Order conditions by importance, only apply top-K conditions
   - Mitigation: Relax conditions if too few candidates remain

**⑥ Top 2 risks from architecture perspective**

1. **硬条件数据可信度风险**: The hard conditions require high-confidence data (augments, items, emblems), but recognition is imperfect. Wrong data → wrong elimination.
   - Mitigation: User confirmation gate for hard conditions, "override" mechanism

2. **过度剔除导致推荐空洞**: If conditions are too strict or too many, the system returns empty/too few results, degrading UX worse than the current system.
   - Mitigation: Minimum candidate floor (always show ≥3 comps), condition relaxation when over-eliminated, tier conditions by criticality

Let me write this up in ≤650 characters... wait, the user said ≤650 字 (Chinese characters), which is about 650 Chinese characters. Let me be concise.

Actually, looking again at the prompt - it says ≤650 字. Let me count carefully and be concise.

Let me structure my response:

① 四个字段不够。缺: 经济门槛(快9需50金、慢D需保息)、血线门槛(<30血不能玩慢成阵容)、人口/阶段门槛(8人口激活羁绊)、已投入棋子冲突(已4哨子难转非哨)。建议加: MinGold, MinHP, MinLevel, BoardCommitment(已投入羁绊标签)。

② 海克斯/纹章/装备三大缺口, 纯V7识别短期不可靠(UI小、图标相似)。**混合方案最贴合"硬剔除"**: 
- 海克斯: 识别+用户三选一确认(影响最大, 必须确认)
- 装备: 优先从棋盘/备战席已装备单位反推(比商店识别容易)
- 纹章: 由海克斯+装备组合推断, 用户可补
理由: 硬剔除需要高置信度数据, 用户声明兜底识别误差。

③ 边界原则: **本局内已不可能满足 → 硬剔除; 仍有可能但未成型 → 标注保留**。
硬剔除: MaxContested超阈值、关键棋子本局已无望2星(5费+阶段4+库存0)、血线<20且阵容需≥2回合启动、关键装备本局已无来源(野怪已过)。
标注保留: 纹章未刷到但海克斯可能给、装备差1-2件可替换、二星未达但能roll。

④ 同行口径分歧:
- "抢同一核心牌" = 精确, 但需新逻辑: 从OpponentSnapshot聚合"持有本阵容关键棋子(≥1个)的不同对手数"
- "抢同一费牌池" = 宽松, 当前 takenByOthers 已近似
建议: MaxContestedPlayers 用"核心牌口径", 与 takenByOthers 解耦但可互验; takenByOthers 仍用于超几何概率的库存扣减。

⑤ 风险与对策:
- 过度剔除→推荐空洞: 设候选下限(≥3), 触发后逐级放宽条件
- 条件数据错误→误剔: 剔除时输出"剔除原因+证据", 用户可一键override
- 组合剔除: 条件按T0/T1分级, 仅T0必剔, T1仅在候选>5时启用

⑥ 架构视角最大2风险:
1. **硬剔除的数据可信度不对称**: 同行/二星来自识别(可信), 海克斯/装备依赖用户声明(易错/懒)。一旦用户漏声明augment, 含emblem依赖的阵容会被误剔, 而这恰是最强阵容。
   对策: 启动推荐前强制augment确认(弹窗或快捷选择), 缺确认则降级为"标注"而非"剔除"。
2. **硬条件字段与推荐算法的耦合位置错配**: 当前草案"算法候选→条件过滤→梯度排序", 但 TacticalAdvisor 已按持有命中数排序, 条件过滤若在排序后做, 会浪费算法算力; 若在排序前, 又可能过滤掉算法认为高分但条件不满足的阵容。
   对策: 算法层生成宽候选(20+), 条件过滤做硬剔除, 梯度排序在剩余候选上做; 条件字段作为算法层的"硬约束"传入, 让算法在生成时就避开明显不满足条件的组合, 减少无效计算。

Let me count characters... this might be too long. Let me tighten it up.

Actually, I should aim for ~650 Chinese characters. Let me be more concise.

Let me rewrite more tightly:

---

**① 字段不够。** 缺四类硬条件: 经济门槛(快9需50金保息、慢D需保50息)、血线门槛(<25血不能玩慢成阵容)、人口/阶段门槛(8人口激活羁绊、5-2前需成型)、已投入羁绊冲突(已4哨子难转非哨)。建议增: MinGold / MinHP / MinLevel / BoardCommitment。

**② 三大缺口, 混合方案最贴合硬剔除。** 纯V7短期不可靠(海克斯UI小、装备图标相似)。分层:
- 海克斯: 识别+用户三选一确认(影响最大, 必确认)
- 装备: 优先从棋盘/备战席已装备单位反推(比商店

---

## Qwen 3.8 Flash (风险)

①四字段不够。补：经济门槛（可D金/利息/升级）、血线容错（能否赌/需连胜或连败）、阶段截止（当前回合是否赶得上成型）、商店库存（核心牌剩余能否2星）。海克斯也应独立为 RequiredRunes，但先三态处理。

②海克斯/纹章/装备缺口：不要等V7。短期用户手动声明+V7辅助；未识别=未知，未知不硬剔。只有明确“无且不可获得”才剔。最贴合可用性的规则：开局/当前面板让用户确认，算法负责推断来源（选秀/野怪/商店）并给证据。

③边界：硬剔=明确不满足且不可补救：需要纹章但已无来源、关键牌同行超上限且剩余库存不足以2星、血线/阶段已错过唯一窗口。标注=未知、可补救、暂时不满足、非核心依赖、二星概率低但库存够。

④同行口径：以“抢同一核心英雄/关键牌”为主，不是同费池。MaxContestedPlayers 看对手快照持有同名核心；同费池交给 takenByOthers 做库存扣减。二者衔接：玩家数超阈值且库存期望低于二星需求才剔；快照缺失时降为标注。

⑤风险：过度剔除→无阵容：过滤后为空时按最小违反数保留Top3并标注“勉强可玩”；错误剔除→三态未知不剔，硬剔需证据链，支持用户覆盖。

⑥最大风险：1）识别缺口永久误剔：未知不剔、手动声明优先、V7补；2）同行误判：核心牌口径+库存概率双验，不单独用玩家数。

---
