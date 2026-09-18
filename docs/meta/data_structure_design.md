# 阵容资讯系统 · 设计定稿 v6(吸收 Supreme Arbiter 终审 + 结构化站位)

> 状态: 定稿候选, 待用户确认后出实现契约。**尚未派发实现。**
> 变更: 在 v5 基础上吸收 Claude Fable 5 终审的 6 项必改 + 用户新增"推荐站位 4×7"需求。

---

## 一、v6 相对 v5 的关键修正(仲裁批注落地)

| # | v5 问题 | v6 修正 |
|---|---|---|
| 1 | 条件字段字符串 DSL(`MinUnitCounts:"蜘蛛>=3"`) | 拆: 可结构化→结构化 record; 不可结构化→显式标 LLM-only 字段 |
| 2 | PlayerDeclaration 三个 List 只能表达"有" | 改 `IReadOnlyDictionary<string, ConditionStatus>`, unknow默认 |
| 3 | 兜底"最小违反数"等权 | 违反分级(致命/可补救/软), 不可补救永不补回 |
| 4 | E4 阵容知识库 vs comps.json 双源 | 明确 comps.json 为单一事实来源, E4 退化为兜底 |
| 5 | 声明破坏回放可重现 | 声明入 MatchLogger 事件流(CorrectionRecorded 类) |
| 6 | LLM 成本治理缺口 | 统一预算池(VLM转录/参谋/复盘共用熔断) |
| 7 | 关联层/知识层死层 | 关联层接入(互转建议); 知识层明确 LLM-only 展示 |
| 8 | 静态需求 vs 动态门槛混装 | 拆 `IntrinsicRequirements`(固有)/`SituationalThresholds`(局势) |
| 9 | 站位是文本 `Positioning` | 新增**结构化 `BoardLayout`(4×7 slot→hero)**(本次需求) |

---

## 二、数据模型(C# record, v6 定稿)

### 2.1 强度与来源
```csharp
public enum CompTier { T0, T1, T2 }
public enum MetaSourceType { UserImage, Manual, ThirdParty }
public sealed record MetaSource(MetaSourceType Type, string Provider, string Patch, string? Url, double Confidence);
```

### 2.2 固有需求 IntrinsicRequirements(一图流转录而来, 阵容固有属性)
```csharp
public sealed record IntrinsicRequirements(
    // 关键棋子二星依赖(空=无): 结构化 单位名+数量
    IReadOnlyList<UnitCountRequirement> RequiredTwoStarUnits,
    // 纹章依赖(空=不依赖)
    IReadOnlyList<string> RequiredEmblems,
    // 装备刚需(空=无刚需): 仅核心英雄必需成装名
    IReadOnlyList<string> RequiredItems,
    // 海克斯门槛(空=无)
    IReadOnlyList<string> RequiredAugments,
    // 牌数量门槛: 结构化(单位, 最少数量), 替代字符串 DSL
    IReadOnlyList<UnitCountRequirement> MinUnitCounts);

// 结构化条件(替代 "蜘蛛>=3" 字符串)
public sealed record UnitCountRequirement(string UnitName, int MinCount);
```

### 2.3 局势门槛 SituationalThresholds(环境相关, 可被策略覆盖)
```csharp
public sealed record SituationalThresholds(
    int? MinGold,          // nullable 表达"未声明", 不用 0 兼任
    int? MinHp,
    int? MinLevel,
    string? ByStage,       // 如 "5-2" 此前需成型
    int? MaxContestedPlayers, // 同行上限(抢同一核心牌); null=不限
    string? BoardConflictNote); // 已投入羁绊冲突, LLM-only 展示字段
```

### 2.4 结构化站位 BoardLayout(新增, 本次核心)
```csharp
// 一个棋子在棋盘上的站位: 4×7 网格坐标(row 0-3, col 0-6)
public sealed record BoardPlacement(string HeroName, int Row, int Col);

// 阵容推荐站位: 4×7 网格(28 槽), 空槽不填
public sealed record BoardLayout(
    IReadOnlyList<BoardPlacement> Placements,   // 有棋子占位的格子
    string? PositioningNote);                   // 站位注意事项(文本, 保留)
```

### 2.5 阵容主体 CompMeta
```csharp
public sealed record CompMeta(
    string Id, string Name, string CompCode,
    IntrinsicRequirements IntrinsicRequirements,
    SituationalThresholds? Situational,      // 可空
    IReadOnlyList<UnitBuild> Units,           // 核心棋子: 名字/费用/出装
    IReadOnlyList<StageStep> Leveling,        // 升人口节奏
    IReadOnlyList<Variant> Variants,          // 变阵(条件→方案)
    string? SettleNote,
    IReadOnlyList<Augment> Augments,          // 海克斯
    IReadOnlyList<TraitThreshold> Traits,     // 羁绊+阈值
    BoardLayout? BoardLayout,                 // 推荐站位(本次新增, 可空=暂无站位数据)
    IReadOnlyList<string> Transformations,    // 质变
    MetaSource Source);

public sealed record UnitBuild(string HeroName, int Cost, IReadOnlyList<string> Items, string? Note);
public sealed record StageStep(string Stage, string Action);
public sealed record Variant(string Condition, string Plan);
public sealed record Augment(string Name, string? Stage, string? Effect);
public sealed record TraitThreshold(string Trait, IReadOnlyList<int> Thresholds);
```

### 2.6 梯度/关联/知识/装备
```csharp
public sealed record TierEntry(
    string CompId, CompTier Tier,
    IReadOnlyList<ConditionalTierShift> Conditionals,  // 条件升降档(v6 改双向)
    string? Note, MetaSource Source);
public sealed record ConditionalTierShift(string Condition, CompTier? ShiftedTier, string? Effect);

public enum RelationKind { Flex, Upgrade, Counter }
public sealed record CompRelation(string FromCompId, string ToCompId, RelationKind Kind, string? Condition, MetaSource Source);

public sealed record Tip(string Id, string? CompId, string Content, double Confidence, IReadOnlyList<string> Evidence, MetaSource Source);
// 注: 装备信息**不设独立强度实体**(不做"出装聚合出强度"的伪统计)。装备以两种形式存在:
//   1. 固定预先录入的推荐出装 = CompMeta.Units 的 UnitBuild.Items(每英雄推荐装备清单, 一图流转录而来);
//   2. LLM 参谋层按需给装备建议(基于 UnitBuild.Items + 棋盘, 自然语言, 经统一预算池)。
```

### 2.7 用户声明(三态修正)
```csharp
public enum ConditionStatus { Has, Missing, Unknown }
// v6 修正: 用 dictionary 表达三态, 每个前置条件键→状态
public sealed record PlayerDeclaration(
    IReadOnlyDictionary<string, ConditionStatus> Conditions);
// 键约定: 如 "augment:裁决转" / "emblem:森林转" / "item:羊刀" / "unit2star:螳螂"
```

---

## 三、推荐链路(v6 修订)

```
GameStateSnapshot + PlayerDeclaration
  │
  ├─[算法层] TacticalAdvisor → 宽候选(持有命中数排序)
  │
  ├─[条件匹配] MatchRequirements(候选, 快照, 声明):
  │     固有条件(IntrinsicRequirements) 逐条匹配;
  │     局势门槛(SituationalThresholds) 用当前 Gold/Hp/Level/Stage/同行数;
  │     三态声明(Has/Missing/Unknown), 违规分级:
  │       致命(纹章缺失且不可得)→ 剔除
  │       可补救(差几件装备)→ 保留标注
  │       软(经济/血线)→ 保留标注
  │     未知 → 保留标注"待确认"
  │
  ├─[兜底] 存活 < 3 → 仅补回"可补救+未知"类违反(Top 补足), 致命违反永不补回
  │
  ├─[排序] 主键=存活状态, 次键=Tier, 三键=本地命中度(排序规则写死入契约)
  │
  ├─[关联增强] 存活候选附 CompRelation(互转/克制提示)
  │
  └─[参谋层] LLMAdvisor(统一预算池): (存活候选+梯度+站位+棋盘)→自然语言建议
```

**关键边界**:
- 本地概率 vs 第三方梯度**分栏展示不合并**; 排序可加权但可解释、可关闭;
- 站位(BoardLayout)进参谋层上下文(防对位建议);
- 视觉 LLM 仅导入时; 推荐热路径只读本地缓存。

---

## 四、存储

```
data/meta/{patch}/
├── version.json          # VersionMeta(梯度榜)
├── comps.json            # CompMeta[](含 IntrinsicRequirements/BoardLayout/UnitBuild.Items 推荐出装)
├── relations.json        # CompRelation[]
├── tips.json             # Tip[]
└── images/{hash}.ext     # 原图归档
data/meta/comp-codes.json # 阵容码总表(供 UI 复制)
```
- **E4 阵容知识库定位**: comps.json 为单一事实来源; E4 的 CompKnowledgeBase 退化为静态兜底(comps.json 未加载时的 fallback), 不再并行维护两份数据。

---

## 五、站位数据来源(本次新增的关键决定)

- 一图流右下角有**站位图**(视觉), v5 将其转录为文本 `Positioning`, 丢失了格坐标。
- v6 将站位**结构化**为 `BoardLayout(slot→hero)`, 数据来源:
  - 主要: 一图流站位图的**视觉转录**(META-INGEST 时单独 OCR/视觉解析站位图 → row/col);
  - 兜底: `PositioningNote` 文本保留(站位图无法结构化时)。
- 动态站位调整(开门/防螳螂/对位)是**后续**需求, 本期只落静态推荐站位。

---

## 六、E4 双源裁定

`CompKnowledgeBase`(现有)= 静态兜底; `comps.json`(MetaInfo)= 单一事实来源。依赖方向: `TacticalAdvisor → MetaInfoStore → JSON`, MetaInfoStore 不反向依赖 TacicalAdvisor/CompKnowledgeBase。

---

## 七、实现 DAG(v6, 定稿后拆分)

| id | 任务 | 依赖 |
|---|---|---|
| META-MODEL | 数据模型 record(v6 全部字段含 BoardLayout) | - |
| META-STORE | MetaInfoStore 加载/校验/查询 + 推荐出装(UnitBuild.Items)加载 | META-MODEL |
| META-INGEST | IMetaIngestor(一图流图片→CompMeta, 含站位图结构化转录; 文字梯度榜→TierEntry+Tip) | META-MODEL |
| META-MATCH | 条件匹配(三态+违规分级+兜底+排序) | META-MODEL, META-STORE |
| META-INTEGRATE | TacticalAdvisor/DecisionPanel 接入梯度排序+条件过滤+站位 | META-MATCH |
| META-UI | 阵容卡(含 4×7 站位渲染 + 阵容码复制) | META-INTEGRATE |

---

## 八、待用户确认

1. 站位 `BoardLayout` 结构化方案(slot→hero + row/col)是否认可;
2. E4 退化为兜底是否认可;
3. 装备改为"固定预先录入的推荐出装(UnitBuild.Items)+ LLM 参谋按需建议", 不做独立强度实体;
4. 声明确认后, 是否现在就出 META-MODEL 契约派发(还是先只确认设计)。

> 注: UI 优化(UI-POLISH)已并行派发, 与本设计无依赖; 本设计的 META-UI 依赖 META-MODEL 等前置, 后续再排。