# 阵容资讯系统 + 一图流导入 研讨 (Live)

> 日期: 2026-09-16 ｜ 模型: DeepSeek V4.1 Flash / MiniMax M3 / Qwen 3.8 Flash

## DeepSeek V4.1 Flash (需求)

我们需要回答用户。需要以【铲瞳 ChanSight】需求与设计委员会成员，研讨主题阵容资讯系统+一图流导入。输出≤600字。需要包含①数据结构建议（字段与类型）；②存储方式；③与现有 CompTemplate/TacticalAdvisor 衔接；④一图流解析接口形态与 LLM 输出契约；⑤需求视角最大2风险与对策。

我们需要在 600 字内 (中文)。要具体。需要结合背景现状。要需求视角覆盖用户可见信息：强势阵容/出装/海克斯/装备强度/匹配度/前中后期/变阵/注意事项。最小必要字段。数据结构建议。存储方式。衔接方案。接口形态和 LLM 输出契约。风险对策。

需要精炼。可以使用 markdown。尽量不超过600字，但内容要足够。中文字数统计？可以约550-600。需要包括字段类型。

思考结构：

① 新增 Meta 层，不要污染 CompTemplate。定义：
- MetaSnapshot { version:string; source:SourceRef; asOf:ISO; patch:string; tierList:TierEntry[]; items:ItemMeta[]; augments:AugmentMeta[]; compMetas:CompMeta[] }
- SourceRef { type:"third_party"|"user_image"|"community"; name; url?; confidence:0-1; parsedBy? }
- TierEntry { compId?/compName; tier:"S"|"A"|"B"|"C"; stage:"early"|"mid"|"late"|"all"; note? }
- CompMeta { key:string; name; aliases:string[]; stages:StagePlan; flex:Variant[]; tips:string[]; source:SourceRef; }
StagePlan { early:Plan; mid:Plan; late:Plan } Plan { coreUnits:string[]; traits:string[]; items:ItemRec[]; augments:AugmentRec[]; position?: string; note?: string }
ItemRec { itemId/name; holder?:string; priority:1-5; matchScore?:0-100; source? }
AugmentRec { name; tier?: string; priority:1-5; score?:0-100; stage?: }
Variant { trigger:string; replaceIn:string[]; replaceOut:string[]; note?:string }
MatchScore separate? 装备强度与匹配度: ItemMeta { name; strength:number; tags[]; compMatch: {compKey, score}[] }.
需求最小字段：tier, stage, comp, units/items/augments, source/confidence.

② 存储：独立 data/meta/{patch}/{version}.json + schema；来源、版本、asOf 必填；代码只读 schema，不硬编码；可支持多来源叠加，按 confidence 和 patch 过滤。用户图片解析结果存 user_uploads/{hash}.meta.json + 原图路径。

③ 衔接：CompTemplate 保持阵容骨架；MetaInfo 独立层通过 compKey/name/aliases 关联，不合并。CompKnowledgeBase.QueryByUnits 增加可选的 MetaFilter/patch，返回模板+meta 摘要；TacticalAdvisor 推荐时先 QueryByUnits，再 MetaService.Enrich：tier 排序、出装/海克斯/变阵提示，强度只展示来源，不混入本地概率。若 comp 无法匹配，创建草稿 CompMeta 供用户确认，不自动写模板。

④ 一图流接口：IVlmClient.ParseCompGuideAsync(image, context) -> CompGuideParseResult。输出契约 JSON schema：
{ schemaVersion, source:{type:"user_image", confidence}, patch?, comps:[{name, aliases, tier?, stages:{early:{units,items,augments,note},mid,late}, variants:[{trigger,replaceIn,replaceOut}], tips[], itemRecs[], augmentRecs[]}], warnings[] }
要求每字段 optional，未知 null；source/confidence 必填；LLM 只抽取不计算，不补造；解析后人工/规则校验，入库前展示确认。

⑤ 风险：
1 来源污染/置信混淆：第三方强度 vs 本地概率。对策：所有 meta 字段带 source/confidence/asOf，UI 分色标注“资讯”；TacticalAdvisor 输出分离“本地匹配”和“外部强度”，不合并为单一概率。
2 一图流幻觉/结构漂移：图片质量差、版本旧、解析错。对策：JSON schema 校验、patch/版本校验、低置信字段置 warning、用户确认后入库，保留原图 hash 可追溯，多源冲突不覆盖只并列。

需求覆盖：用户可见：强势阵容列表（tier, patch, source），出装（item holder priority matchScore），海克斯（name tier stage priority），装备强度/匹配度（strength, compMatch），前中后期（StagePlan），变阵（trigger/replace），注意事项（tips）。这些在数据结构和衔接

---

## MiniMax M3 (架构)

The user is asking me to act as a member of the "铲瞳 ChanSight" requirements and design committee. The topic is about a lineup/composition information system + "one-image flow" (一图流) import.

Let me analyze the context carefully:

**Current code facts:**
- CompTemplate (Id/Name/KeyTraits/CoreUnits/SecondaryUnits/Description) + CompKnowledgeBase.QueryByUnits sorted by "hero match count"
- TacticalAdvisor lineup recommendation only consumes QueryByUnits, no strength/meta/preference concept
- IVlmClient interface supports image input (multimodal), Vision side has OpenRouterVlmClient
- GameSeasonDictionary has Heroes/Traits/Items three dictionary tables
- Fixed ROI calibrated (board 28 cells/bench 9/shop 5/gold/level/stage/HP)

**User requirements:**
1. Add lineup strength grading
2. Users can pass images (一图流), LLM reads front/mid/late game lineup recommendations, variations, notes, recommended items
3. System maintains an information system including: current strong lineups list, recommended items, strong Hexes (海克斯), equipment strength and matchability; these info usually tested by third parties, system should flexibly use (not self-calculate strength)

**Key difficulties:**
- 一图流 is "image guide" (unstructured), needs LLM vision + semantic parsing into structured data; what should the data structure look like?
- Front/mid/late game lineups, variations, notes, items, Hexes, equipment strength, matchability - what are the forms (enum/list/weighted score/association table), how to model?
- How to store (JSON files / in-code constants / independent data directory / whether need version number and source annotation)?
- How does info system connect with existing CompKnowledgeBase/CompTemplate/TacticalAdvisor, extend existing types or add independent MetaInfo layer?
- "Third-party tested info" how to label source/confidence, avoid confusion with locally calculated probability?

**My perspective:** Architecture perspective: data structure and storage solution (JSON directory/constants/independent MetaInfo layer), dependency direction and extension points with existing CompKnowledgeBase/CompTemplate/TacticalAdvisor, avoid circular dependencies; 一图流 LLM parser interface contract and output JSON schema.

**Output ≤600 words:**
1. Data structure suggestion (specific to fields and types)
2. Storage method
3. Connection scheme with existing CompTemplate/TacticalAdvisor
4. 一图流 parsing interface form and LLM output contract
5. Top 2 risks from your perspective and countermeasures

Let me think carefully about this as an architecture-focused committee member.

**① Data structure suggestions:**

Need to design:
- Lineup strength tier (S/A/B/C)
- Lineup stage composition (front/mid/late)
- Variations (变阵)
- Notes (注意事项)
- Recommended items
- Strong Hexes (Augments/海克斯)
- Equipment strength & matchability

I should propose specific fields and types. Let me design:

```csharp
// Meta tier enum
public enum CompTier { S, A, B, C, D }

// Stage enum
public enum GameStage { Early, Mid, Late }

// Source annotation
public record MetaSource(string Provider, string Patch, DateTime CollectedAt, string Url = null, float Confidence = 1.0f);

// Lineup meta info
public record CompMeta(
    string CompId,           // FK to CompTemplate.Id
    CompTier Tier,
    GameStage StrongestStage,
    List<StagePlan> Stages,
    List<string> Variations, // CompIds of variants
    List<string> Notes,
    List<ItemRecommendation> Items,
    List<AugmentRecommendation> Augments,
    MetaSource Source
);

public record StagePlan(
    GameStage Stage,
    List<string> CoreUnits,    // Hero IDs
    List<string> KeyTraits,
    int BoardPriority           // 1-28 grid hint
);

public record ItemRecommendation(
    string ItemId,              // FK to GameSeasonDictionary.Items
    string HeroId,              // FK to Heroes
    float Priority,             // 1-5 or 0-1
    List<string> Alternatives
);

public record AugmentRecommendation(
    string AugmentId,           // need new dictionary
    CompTier Tier,              // S/A/B/C
    List<string> SynergyCompIds // comps that benefit
);

// Equipment strength (cross-cutting)
public record EquipmentMeta(
    string ItemId,
    float PowerScore,           // 0-100
    List<string> BestHeroIds,
    List<string> AcceptableHeroIds,
    MetaSource Source
);
```

**② Storage method:**

- JSON files in independent data directory: `Data/Meta/{Patch}/comp-meta.json`, `Data/Meta/{Patch}/items-meta.json`, `Data/Meta/{Patch}/augments-meta.json`
- Versioned by patch (e.g., "14.1", "14.2")
- Source annotation embedded in each record
- Could also have code-side constants for default/fallback values
- Why not code constants: third-party info updates frequently, JSON easier to update without recompile

**③ Connection with existing CompTemplate/TacticalAdvisor:**

- CompTemplate stays as "static structural template" (Id/Name/KeyTraits/CoreUnits/SecondaryUnits/Description)
- Add new layer: `IMetaInfoProvider` / `MetaInfoService` that loads JSON
- CompKnowledgeBase gets new methods: `QueryByMetaTier(tier)`, `GetMetaFor(compId)`
- TacticalAdvisor gets extended to optionally consume meta info:
  - New parameter `MetaPreference` (prefer S-tier, prefer early game, etc.)
  - When meta info available, use it; when not, fall back to current QueryByUnits
- Dependency direction: TacticalAdvisor → MetaInfoService → JSON files; MetaInfoService does NOT depend on TacticalAdvisor (no circular)

**④ 一图流 parsing interface:**

```csharp
public interface IMetaImageParser
{
    Task<MetaParseResult> ParseAsync(Stream image, string patch, CancellationToken ct);
}

public record MetaParseResult(
    bool Success,
    string Patch,
    List<CompMetaDraft> Comps,
    List<ItemRecommendationDraft> Items,
    List<AugmentRecommendationDraft> Augments,
    List<string> Warnings,
    string RawModelOutput  // for debugging
);

public record CompMetaDraft(
    string Name,
    CompTier Tier,
    List<StagePlanDraft> Stages,
    List<string> VariationNames,
    List<string> Notes
);
```

LLM output contract (JSON schema):
- Use System.Text.Json or Newtonsoft
- Provide schema to LLM via prompt
- Validate against schema, return errors if invalid
- Then map Drafts → CompMeta (resolving hero/item names to IDs via GameSeasonDictionary)

**⑤ Top 2 risks:**

Risk 1: LLM hallucination - LLM might invent hero names, item names, or wrong tiers
- Countermeasure: Strict name→ID resolution via dictionary, fail-loud on unknown names; require source URL in prompt; cross-validate with multiple images if possible

Risk 2: Patch version drift - Meta info tied to specific patch, but dictionary (Heroes/Items) might not have all names
- Countermeasure: Version every meta JSON by patch; dictionary lookup must be patch-aware; gracefully degrade when name not found (keep as raw string in "Unknown" bucket for human review)

Let me write this up concisely in ≤600 words (Chinese).

I need to write this as a committee member, so use professional but conversational tone. Use bullet points and structured formatting. Output must be ≤600 字 (Chinese characters).

Let me draft:

---

**① 数据结构建议**

新增独立 `Meta` 命名空间，不侵入 CompTemplate：

```csharp
enum CompTier { S, A, B, C, D }
enum GameStage { Early=1, Mid=2, Late=3 }

record MetaSource(string Provider, string Patch, 
                  DateTime CollectedAt, string Url=null, float Confidence=1.0f);

record CompMeta(string CompId, CompTier Tier, 
                GameStage StrongestStage,
                List<StagePlan> Stages,
                List<string> VariationCompIds,
                List<string> Notes,
                List<ItemRec> Items,
                List<AugmentRec> Augments,
                MetaSource Source);

record StagePlan(GameStage Stage, List<string> CoreUnits, 
                 List<string> KeyTraits, int BoardPriority);

record ItemRec(string ItemId, string HeroId, 
               float Priority, List<string> Alternatives);

record AugmentRec(string AugmentId, CompTier Tier, 
                  List<string> SynergyCompIds);

record EquipMeta(string ItemId, float PowerScore, 
                 List<string> BestHeroIds, MetaSource Source);
```

CompTemplate 保持纯结构模板，Meta 是"外挂层"。

**② 存储方式**

`Data/Meta/{Patch}/` 目录：
- `comp-meta.json` / `items-meta.json` / `augments-meta.json` / `equipment-meta.json`
- 每条记录内嵌 `MetaSource` 标注来源/Patch/置信度/URL
- 不放代码常量：第三方数据更新频繁，JSON 热更新无需重编译
- 加载器 `MetaInfoLoader` 缓存到内存，启动时校验 schema

**③ 与现有体系衔接**

依赖方向（无环）：
```
TacticalAdvisor → MetaInfoService → JSON
                ↘ CompKnowledgeBase (现有)
```
- `IMetaInfoProvider` 新接口：`GetMeta(compId)` / `QueryByTier(tier, stage)` / `GetEquipScore(itemId, heroId)`
- `CompKnowledgeBase` 新增 `QueryByMeta(...)` 重载，**不**改 `QueryByUnits` 签名
- `TacticalAdvisor` 增加可选 `MetaPreference` 参数（用户偏好 S 阶/前排/特定海克斯），有 Meta 时加权，无 Meta 时 fallback 到 `QueryByUnits`
- 关键：MetaInfoService **不引用** TacticalAdvisor/CompKnowledgeBase，只引用 Dictionary，避免反向依赖

**④ 一图流解析接口**

```csharp
interface IMetaImageParser {
    Task<MetaParseResult> ParseAsync(
        Stream image, string patch, CancellationToken ct);
}

record MetaParseResult(bool Ok, string Patch,
    List<CompMetaDraft> Comps, 
    List<ItemRecDraft> Items,
    List<AugmentRecDraft> Augments,
    List<string> Warnings, string RawJson);
```

LLM 输出契约（prompt 内嵌 JSON Schema）：
- 顶层 `{patch, comps[], items[], augments[]}`
- `comp`: `{name, tier, stages:[{stage, coreUnits[], keyTraits[], boardPriority}], variationNames[], notes[]}`
- `items`: `{itemName, heroName, priority, alternatives[]}`
- `augments`: `{augmentName, tier, synergyCompNames[]}`
- **全部用 Name 而非 ID**，解析后由 `NameResolver` 对照 `GameSeasonDictionary` 转 ID，未知名进 `Unknown` 桶供人工复核

**⑤ 两大风险与对策**

1. **LLM 幻觉/编造英雄名/装备名/强度**
   - 对策：强 schema 校验 + 字典白名单；未知名不丢弃，进 `Unknown` 桶并标 `Confidence=0

---

## Qwen 3.8 Flash (风险)

我们需要回答用户要求：作为反方风险视角，输出 ≤600 字，包含①数据结构建议(具体到字段与类型);②存储方式;③与现有 CompTemplate/TacticalAdvisor 的衔接方案;④一图流解析的接口形态与 LLM 输出契约;⑤你视角最大的 2 个风险与对策。

需要用中文。必须控制字数 ≤600字。需要具体。考虑现有代码事实：CompTemplate Id/Name/KeyTraits/CoreUnits/SecondaryUnits/Description；CompKnowledgeBase.QueryByUnits；TacticalAdvisor；IVlmClient；OpenRouterVlmClient；GameSeasonDictionary；ROI。需求：强度分级，图片一图流解析前中后期阵容推荐、变阵、注意事项、推荐出装，资讯系统当前强势阵容列表、推荐出装、强势海克斯、装备强度与匹配度。第三方测试信息，不自行计算强度。

反方风险视角：强调解析误读、第三方过时/不可信、成本、迁移冲突。数据结构建议要谨慎，不盲目覆盖本地。

需要 ≤600字。可以压缩。字段类型具体。

建议：
1 数据结构：
- CompMetaInfo: Id, CompTemplateId, SeasonId, SourceType(enum Manual/Image/Remote), SourceRef, Version, Confidence(decimal), Tier(enum S/A/B/C), WinRate?, PickRate?, PowerScore? maybe nullable, UpdatedAt, Status(enum Draft/Active/Deprecated)
- StageLineup: CompMetaId, Stage(enum Early/Mid/Late), Units(list<string>), Traits(list<string>), Items(list<ItemSuggestion>), Notes(string)
- VariantRule: CompMetaId, TriggerUnits/TriggerItems/TriggerHexes, ReplaceIn/ReplaceOut, Priority
- ItemSuggestion: Hero, Item, Slot, MatchScore?, Source
- HexSuggestion: Hex, Priority, MatchScore?
- EquipmentStrength? 或 ItemMeta: ItemId, Strength(enum), Role(enum), MatchTraits(list<string>)
- RawParseResult maybe Json, Hash, ImageRef.

存储：独立 data/meta/{season}/*.json，版本化，source标注，不塞代码常量；导入生成 Draft，人工审核/规则校验后 Active。图片原图存 data/images/{hash}.jpg，解析JSON存 parsed/{hash}.json，可追溯。

衔接：新增 MetaCatalog/CompMetaProvider；CompTemplate 保持核心结构，不扩展强字段；TacticalAdvisor 输入 Units+持有海克斯/阶段/金币，先本地 QueryByUnits 得候选，再用 MetaCatalog 按 CompTemplateId 查 Tier/StageLineup/Items/Hex，排序用 meta Tier + 本地命中，不覆盖本地概率。迁移：CompTemplate 加 MetaId 可选或映射表 CompTemplateMetaMap，避免破坏。

接口：ICompMetaIngestor.ParseImage(imageRef, season, promptVersion) -> ParseResult{Status, Errors, Confidence, DraftCompMeta, RawJson}; LLM输出严格JSON Schema: {comps:[{name,tier,source_confidence,early:{units,traits,items,notes},mid:{...},late:{...},variants:[{when,swap_in,swap_out}],hexes:[{name,priority}],items:[{hero,item,slot,match}],warnings:[],unknown:[]}]; require enum, dictionary normalization, no free text units; max tokens; temperature 0; validate against GameSeasonDictionary.

最大2风险与对策：
- 误读/幻觉：只接受 Draft，必须命中字典≥阈值，低置信/字段缺失丢弃或人工复核；展示来源和置信度，不自动推荐。
- 第三方过时/冲突：Version+Source+UpdatedAt+Status，过期降权；本地概率与资讯分开展示，排序仅资讯做偏好，强度不写回 CompTemplate；成本：图片hash缓存，批量导入，人工审核。

Need <=600 Chinese chars maybe 600字. Let's draft concise.

Draft:
①数据结构：
CompMeta{Id,CompTemplateId,SeasonId,Source(Image/Remote/Manual),SourceUrl,Version,Confidence,Tier(S/A/B/C),UpdatedAt,Status(Draft/Active/Deprecated),RawJsonHash}
StageLineup{CompMetaId,Stage,Units[],Traits[],Items[],Notes}
Variant{CompMetaId,When,SwapIn[],SwapOut[],Priority}
ItemSuggestion{Hero,Item,Slot,MatchScore?,Source}
HexSuggestion{Hex,Priority,MatchScore?}
ItemMeta{Item,Strength,Role,MatchTraits[]}
②存储：data/meta/{season}/*.json，版本/来源；原图data/images/{hash}，解析结果data/parsed/{hash}.json；不写代码常量，导入先Draft后审核Active。
③衔接：新增MetaCatalog，CompTemplate不扩强字段，可选MetaId或映射表；TacticalAdvisor仍QueryByUnits得候选，再按MetaCatalog Tier/Stage/Hex/Items排序与提示；本地概率与第三方资讯分栏，资讯不覆盖计算。
④接口：ICompMetaIngestor.ParseImage(imageRef,season,promptVer)->ParseResult{Status,Errors,Confidence,DraftMeta,RawJson}。LLM契约严格JSON：comps[{name,tier,early/mid/late{units,traits,items,notes},variants,hexes,items,warnings,unknown}]；枚举/字典校验，temp0，低置信拒。
⑤风险：误读/幻觉→仅Draft、字典命中阈值、人工复核、显示来源置信。过时冲突→Version/UpdatedAt/Status过期降权，资讯与本地概率隔离，不自动改Tier；图片hash缓存降成本。

Count roughly: Chinese characters maybe 400? Need <=600. Good. But user asked 输出 ≤600 字: ①...; we can include bullets. Need ensure not too long. Let's count approximate: ①数据结构：... maybe 250. ②存储... 70. ③... 100. ④... 130. ⑤... 80. total 630? Need trim. Let's make tighter.

Final:
①数据结构：CompMeta{Id,CompTemplateId?,SeasonId,Source(Image/Remote/Manual),SourceUrl,Version,Confidence,Tier(S/A/B/C),Status(Draft/Active/Deprecated),UpdatedAt,RawHash}; StageLineup{CompMetaId,Stage(Early/Mid/Late),Units[],Traits[],Items[],Notes}; Variant{When,SwapIn[],SwapOut[],Priority}; ItemSuggestion{Hero,Item,Slot,MatchScore?}; HexSuggestion{Hex,Priority,MatchScore?}; ItemMeta{Item,Strength,Role,MatchTraits[]}。
②存储：data/meta/{season}/*.json；原图data/images/{hash}，解析data/parsed/{hash}.json；保留版本/来源，导入先Draft，审核Active。
③衔接：新增MetaCatalog；CompTemplate不扩强字段，可选MetaId或映射表；TacticalAdvisor仍QueryByUnits取候选，再按MetaCatalog Tier/阶段/海克斯/出装排序与提示；本地概率与资讯分栏，资讯不覆盖计算。
④接口：ICompMetaIngestor.ParseImage(imageRef,season,promptVer)->ParseResult{Status,Errors,Confidence,DraftMeta,RawJson}。LLM严格JSON：comps[{name,tier,early/mid/late{units,traits,items,notes},variants,hexes,items,warnings,unknown}]；字典校验、temp0、低置信拒。
⑤风险：误读/幻觉→仅Draft、字典命中阈值、人工复核、显示来源置信；过时冲突→Version/UpdatedAt/Status过期降权，资讯与本地概率隔离，不自动改Tier；图片hash缓存降成本。

Count maybe 520. Good. Ensure fields type specific. Add types? We can include types in JSON-like. Need mention enum/list/weighted? We have. Could include "MatchScore? decimal". Maybe enough.

Need final only answer. Ensure ≤600 Chinese chars. Let's count more carefully:
①数据结构：CompMeta{Id,CompTemplateId?,SeasonId,Source(Image/Remote/Manual),SourceUrl,Version,Confidence,Tier(S/A/B/C),Status(Draft/Active/Deprecated),UpdatedAt,RawHash}; StageLineup{CompMetaId,Stage(Early/Mid/Late),Units[],Traits[],Items[],Notes}; Variant{When,SwapIn[],SwapOut[],Priority}; ItemSuggestion{Hero,Item,Slot,MatchScore?}; HexSuggestion{Hex,Priority,MatchScore?}; ItemMeta{Item,Strength,Role,MatchTraits[]}。
This is long. Count: ①(1)数据结构：5? Let's approximate characters: "①数据结构：" 6? Actually Chinese chars: 数 据 结 构 ： =5 plus ①=1. Then CompMeta... English letters count as words? User says 字 maybe Chinese characters, but total should be safe. Need less than 600 likely. Our content maybe 550 including English? Could be over if counting all tokens? Better trim.

Can use shorter:
①结构：CompMeta{Id,CompTemplateId?,SeasonId,Source,SourceUrl,Version,Confidence,Tier,Status,UpdatedAt,RawHash}; StageLineup{CompMetaId,Stage,Units[],Traits[],Items[],Notes}; Variant{When,SwapIn[],SwapOut[],Priority}; ItemSuggestion{Hero,Item,Slot,MatchScore?}; HexSuggestion{Hex,Priority,MatchScore?}; ItemMeta{Item,Strength,Role,MatchTraits[]}。

②存储：data/meta/{season}/*.json；原图data/images/{hash}，解析data/parsed/{hash}.json；版本/来源，导入Draft，审核Active。

③衔接：新增MetaCatalog；CompTemplate不扩强字段，可选MetaId/映射；TacticalAdvisor先QueryByUnits，再按Tier/阶段/海克斯/出装排序提示；本地概率与资讯分栏，不覆盖。

④接口：ParseImage(imageRef,season,promptVer)->ParseResult{Status,Errors,Confidence,DraftMeta,RawJson}；LLM严格JSON：comps[{name,tier,early/mid/late{units,traits,items,notes},variants,hexes,items,warnings,unknown}]；字典校验、temp0、低置信拒。

⑤风险：误读/幻觉→仅Draft、字典命中阈值、人工复核、显示来源置信；过时冲突→Version/Status过期降权，资讯与本地概率隔离，不自动改Tier；图片hash缓存降成本。

This is around 430. Good. Need include enum values? Maybe Tier(S/A/B/C), Source(Image/Remote/Manual), Stage(Early/Mid/Late), Status(Draft/Active/Deprecated).

---
