# E0 遗产盘点: 资产清单 · 设计坑清单 · 止损建议

> E0 只读分析输出, 对应契约 `scripts/tasks/E0_contract.md`。
> 输入: 归档 WIP `%TEMP%/opencode/m4_engine_wip_20260912`、旧计划 `plan_milestone_4.md`、旧委员会评审 `discussion_plan_m4_review_live.md`、主干 `src/ChanSight.Core/` 与 `GameSeasonDictionary.cs`。

---

## E0-1 资产清单(归档 WIP 可复用项逐项)

评级约定: ★★★ 直线采纳 / ★★ 可复用需改造 / ★ 放弃或仅参考思路。

### Engine(核心引擎)

| # | 类/文件 | 定位 | 可复用于 | 评级 | 说明 |
|---|---|---|---|---|---|
| 1 | `HypergeometricEngine.cs` + `IHypergeometricEngine.cs` | 超几何概率引擎 | **E1** | ★★ | 已有支撑件(精确二项系数、`ProbabilityMass`/`CumulativeProbabilityAtLeast`/`ExpectedValue`/`ShopHitProbability`)可直接迁入 E1; 但高层 `CalculateRollProbability*` 用 `1-(1-K/N)^(5·rolls)` 做**无放回近似**, 且 `poolSize = costPool * 29` 硬编码错误(29 是无效总种类数), 与 E1 定案(XIII 种数、等级→费用条件概率"分层先定费用再放回超几何"、手算等式 `1-C(N-5,5)/C(N,5)`)相抵触——重写。 |
| 2 | `GameStateManager.cs` + `IGameStateManager.cs` | 状态机 + 对手快照缓存 | **E2/E3** | ★★ | `lock` + `record` 不可变快照 + `with` 原子替换方向正确; 但对手槽用 `OpponentSnapshot?[]` 数组 + 值拷贝集合, 与 E2 定案要的"带版本号/单调递增、7 对手无锁发布"尚有差距; E3 的去重/切屏检测缺失。可改造复用骨架。 |
| 3 | `TacticalAdvisor.cs` + `ITacticalAdvisor.cs` | 规则决策 | **E5**(参考) | ★ | 硬编码阈值 + 魔法字符串(中文句子直接返回)与 E5 定案(纯算法 + 结构化输出 + 证据置信层 + golden case)冲突; 仅 `RecResult`/`TacticalRecommendation` 的 Action/Priority 枚举思路可参考, 主体放弃。 |

### 模型(Models)

| # 类 | 定位 | 可复用于 | 评级 | 说明 |
|---|---|---|---|---|
| 4 | `GameStateSnapshot.cs` | 不可变全局快照 | **E2** | ★★★ | record + init + 默认空集合, 字段(Stage/Gold/Level/Exp/Hp/Phase/Streak/StreakCount/Board/Bench/Shop/Traits/Opponents)与 E2 需求基本吻合; 缺版本号/时间戳字段(需补)。 |
| 5 | `OpponentSnapshot.cs` | 对手快照 | **E3** | ★★ | 字段 Id/Name/Level/Hp/Board/Bench/Traits/LastSeenStage 可直接用; 但类改用 `class`(非 record)、集合浅拷贝 `IReadOnlyList` 可能引用逃逸, 与评审"深不可变/值对象,version 字段"冲突, 建议改 record + 补 version。 |
| 6 | `UnitSnapshot.cs` | 棋子快照 | **E2/E3/E5** | ★★★ | 名称/费用/星级/装备, 参数校验齐全; 与 `GameSeasonDictionary.Heroes` 命名一致(亚索/盖伦等), 直接采纳。 |
| 7 | `ShopCardSnapshot.cs` | 商店卡 | **E2** | ★★★ | 名称+费用 + 校验, 简单干净, 直接采纳。 |
| 8 | `GamePhase.cs` | 阶段枚举 | **E2** | ★★★ | Planning/Combat/Carousel/PvE 与 DAG E2 状态机一致, 直接采纳。 |
| 9 | `StreakType.cs` | 连胜/连败 | **E2/E5** | ★★★ | None/Win/Loss, 直接采纳。 |
| 10 | `TacticalRecommendation.cs` | 建议 record + Action/Priority 常量 | **E5** | ★★ | Action/优先级枚举(LevelUp/Roll/BuyUnit/…, Critical/High/…)可作为 E5 输出结构参考; 但需并入"建议+理由+风险分+置信层"的新 schema。直接对齐 E5 定案。 |
| 11 | `RecResult.cs` | 建议容器 | **E5** | ★ | 字符串化(不同)滞后, 与 E5 结构化输出不符, 放弃(或重构为强类型)。 |

### 测试(tests/ChanSight.Tests/Engine/)

| # 文件 | 定位 | 可复用于 | 评级 | 说明 |
|---|---|---|---|---|
| 12 | `HypergeometricEngineTests.cs` | 超几何引擎测试 | **E1** | ★★ | 覆盖二项系数/池大小/商店概率/单调性/边界已备, 思路可直接迁; 但用例按旧 `CalculateRollProbability`(近似)设计, 需按 E1 公式(手算 `1-C(N-k,5)/C(N,5)`)重写断言。 |
| 13 | `GameStateManagerTests.cs` | 状态机/快照测试 | **E2/E3** | ★★ | 覆盖默认态/7 对手槽/并发 read/write; 并发用例可用, 但需升级为"版本号单调 + 无数据竞争"校验。 |
| 14 | `TacticalAdvisorTests.cs` | 战术测试 | **E5** | ★ | 硬件 阈值断言与 E5 golden case 矩阵不一致, 主体重写。 |

> 总结: 可**直线采纳**的是纯数据模型(6~9)与快照子集(4); **可改造**的是超几何几何件(1 后半)、状态机骨架(2)、对手快照(5)、测试骨架(12/13); **放弃/重写**的是近似概率主方法、战术主规则(3)。

---

## E0-2 设计坑清单(提炼自旧评审 + 现状核对)

数学 / 概率

- **无放回多槽联合概率**: 商店 5 槽是同复杂、无放回抽取, **不可**用独立二项/`(1-p)^m` 近似; 必须
  超几何 `P(X=k) = C(K,k)·C(N-K,n-k)/C(N,n)`, 至少一张 = `1-P(X=0)`。
- **层级→费用分层两个概率**: 每槽先按等级概率表决定费用, 再从该费用卡池叫超几何; 最终是条件概率乘积/求和 `Σ_c p_c(L)·P(hypergeo|c)`, 不能直接套全区总。
- **同行扣减时序**: 对手已持有(上场+备战+已定)必须从 K 精确扣减; 扣减需在"商店刷新前"统一快照, 避免 vs 后的陈旧数据混入。
- **卖卡归还时序**: 对手卖卡需即时归还牌库; 若归还晚于本回合刷新, 则本回合大概率不变——明确"归还时机"边界。
- **合成(3星)扣减倍数**: 1星/2星/3星分别占 1/3/9 张同名卡, 扣减时按星数折算而非按 1 计。
- **卡池容量常数**: 归档"`×29`"错误; 无误: 1费22/2费20/3费17/4费10/5费9(卡池数量) × 各费种类数(1-3费13/4费12/5费8)。
- **数值稳定性**: N 较大时阶乘乘法下溢; 用对数累加或互补事件(`1-P(0)-P(1)`)计算; 组合数用精确 long。
- **防御性/边界**: K=0 → 0; K=N → 1; 剩余<n → 0; n>N 需截断; 结果限 [0,1] 避免 NaN。单调性(剩余复制越多, 概率越高)必须保持。

并发 / 不可变快照

- **不可变快照原子替换**: 用 record/值对象 + `AtomicReference`/`Reference` 全量替换 — 禁止 mutable 共享。
- **快照版本号 + 时间戳**: 协会单调递增版本号 + `captured_at`, 决策时检查新鲜度/是否 stale。
- **深层不可变**: `sealed record` 需查内部集合不可逃逸(ImmutableList / 防御性拷贝); 防引用泄露。
- **写者-读者一致**: 决策引擎一次 `get()` 取得一致快照; 多个对手应绑定同一逻辑帧。
- **时序检查点**: 仅在 `PreparationPhase.End()` 冻结快照; 更新在商店刷新/战斗结算后。
- **ABA 防护**: 版本号单调递增, 用 EBR / 引用计数防回收。

战术 / 规则

- **得分经济学 vs 概率两项独立区分**: 利息/升人口(经济)与 D 牌命中(概率)是不同维度, 必须分开建模, 不可混一个公式。
- **阈值参数化**: 利息 50、升人口系数等不能硬编码, 应做成默认参数/按阵容类型动态调。
- **规则冲突/优先级**: 存利息 vs 升人口 vs 刷牌需显式优先级(保血量/保连胜>吃利息>升人口>刷牌)。
- **Result 模式**: 规则返回 Result(成功/失败+原因)而非抛异常; 输入负/满级/空背包等前置校验。
- **安全降级**: 给不出决策时执行最保守动作(吃利息), 不硬判。
- **装备合成优先级图**: 核心件 > 过渡件 > 散件; 无路径时用散件; 避免循环建议(合成 A 又需 B)。

侦察 / 数据

- **行侦察去重**: 切屏末端同一对手多次出现应去重(按 LastSeenStage/版本号), 不要重复占用槽位。
- **对应更新**: 切屏识别非己方棋盘时正确映射到 7 对手槽中(棋盘阵营对位)。

---

## E0-3 止损(直线采纳 / 过时 / 缺失)

### 直线采纳(无冲突, 直接进当期)

- 模型层: `UnitSnapshot`、`ShopCardSnapshot`、`GamePhase`、`StreakType`(★三重全对)。
- `GameStateSnapshot` 的主体字段结构(补版本号字段即可)。
- 超几何引擎的底层数学件: `BinomialCoefficient`(精确)、`ProbabilityMass`、`CumulativeProbabilityAtLeast`、`ExpectedValue`、`ShopHitProbability` — 迁入 E1。
- 测试骨架: 超几何单调性/边界、状态机并发 read/write 框架。

### 已过时(与当前 V/M4 定案冲突, 勿采纳)

- **旧计划 M4 的目标描述**("毫秒级中枢"、"7 对手侦察"、"保姆级建议" **"经 LLM 概率公式 `P(X≥n)`")已被 `plan_m4_rebuild_draft.md` 架构定案取代: 决策核心 = **纯算法**(零成本/可回溯), LLM 只做外围参谋的 E5b; 旧 E4 的装备/海克斯/锁牌二期延后。
- **旧 `HypergeometricEngine.CalculateRollProbability*`**: 无放回用放回近似 + `poolSize*29` 错误, 与 E1 的 "分层费用条件概率 + 精确组合" 冲突, 重写。
- **旧 `TacticalAdvisor`**: 硬编码阈值 + 魔法字符串返回, 与 E5 定案(结构化建议 + 理由 + 置信)冲突; `RecResult`/`TacticalRecommendation` 需改为强类型。
- **`OpponentSnapshot` 用 class + 浅拷贝集合**: 未满足"深不可变 + 版本号"的并发共识。
- **旧评审里"Java"优先方案**: 本项目是 C#, 采用 C# 惯用 `record`/`Immutable*`/`lock`。

### 缺少(需补齐, 影响 E1/E5 达成)

- **证据置信层**: 当前 WIP 完全缺失 — E5 所有建议需带"证据/置信/风险分"; 旧 set 无此机制, 需在 E5 新建。
- **golden case 反例集**: 旧测试只有正向阈值, 缺失反例决定矩阵(残血被迫 D、高经济不该 D、同行抢 X、牌库将被释放、该锁牌却非 D)。这是 E5 验收硬性要求。
- **玩法库容量种类常数校验**: E1 的 XLSX 种数(1-3费13/4费12/5费8)未体现于 WIP; WIP 硬编码 29 要净错误, 未注入的 `PoolConfig` 缺失(E1 要求 `PoolConfig` record + 校验)。
- **版本号 / 时间戳 / 去重**: E2/E3 均缺 `version`/`caped_at` 与对手侦察去重逻辑。
- **`IVisionInputAdapter` 接口**(E2 依赖): 无任何定义 — 确定。
- **数据流图 / 接口契约**: 旧评审与重建草案均要求"概率输出→战术输入"的数据流契约, 尚未落实。