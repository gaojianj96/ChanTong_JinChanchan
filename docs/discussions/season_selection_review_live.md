# 赛季/模式 + 字典自学习 · 委员会审议 (Live)

> 日期: 2026-09-19 ｜ 模型: DeepSeek V4.1 Flash / MiniMax M3 / Qwen 3.8 Flash

## DeepSeek V4.1 Flash (需求)

①方向成立，但最大缺陷：把“模式”当字典隔离维度、把“回顾确认”直接等于“正式收录”。模式多半只影响可用池/羁绊/阵容，不应复制英雄/装备字典；否则 S18 匹配/狂暴两套字典维护分裂。最大遗漏：赛季-patch-模式映射、别名/纠错与新增未分离、候选聚合与回滚。

②接口建议：
`ISeasonDictionaryProvider { SeasonModeKey Key; IReadOnlyCollection<string> GetNames(EntityType t); bool TryResolve(EntityType t,string raw,out ResolveResult r); IDisposable BeginScope(SeasonModeKey key); }`
`ResolveResult` 区分 canonical/alias/fuzzy/new。纯算法保留 static，但改为传候选集：`TryFuzzyMatch(IEnumerable<string> candidates,...)`。8 引用点：ViewModel/服务构造注入 provider；static 工具调用处显式传候选。不要全局 `Current`，用 scoped/显式参数，否则回顾与实时会串赛季。可行，但需统一 EntityType 与缓存 key。

③自学习应复用 REVIEW 金标，但确认动作必须三选：映射已有、新增、忽略。只有“新增”写正式字典；映射已有写 alias/纠错，不新增。金标是帧级，字典是赛季级，正式收录前按赛季+实体去重，事务化写金标/纠错/字典，可回滚。候选 pending 持久化并按对局聚合。冲突：现有确认若只有对/错，需扩展。

④顺序：一图流/官方基础字典冷启动，权威；自学习补漏；网络搜索仅建议。网络搜索必须人审+缓存，缓存 raw、解析结果、来源 URL、时间、赛季/patch，不能自动入正式。按赛季+模式+实体查询，限流合规。

⑤最大风险：跨赛季串英雄——单例/缓存未带 key。对策：所有缓存 key 含 SeasonModeKey+字典版本，切换时清识别管线。自学习污染——OCR 噪声、皮肤名、错字被当英雄。对策：频次+置信度+多帧+人工确认，alias 与 canonical 分离，版本化可回滚。

---

## MiniMax M3 (架构)

The user is asking me to act as a member of the "铲瞳 ChanSight" requirements and design committee, reviewing a design draft about Season/Mode selection + Dictionary self-learning. I need to be critical, not just agreeable, and provide specific solutions within 650 characters (Chinese).

Let me analyze each point carefully:

**①方案是否成立, 最大缺陷/遗漏**
The overall approach is sound but has issues:
- Mode dimension is added to dictionary but MetaInfo is only patch-aligned - there's a mismatch (mode vs patch)
- The 8 reference points all currently use `GameSeasonDictionary.Heroes` directly - some are in async services, some in static methods (like `ReplayCommand` which is likely a static command). Refactoring static method call sites to use DI is non-trivial.
- Self-learning "candidate" storage - where does it live? Per-session? Per-season? Race conditions?
- The "三合一" (write gold standard + formal inclusion + correction) at review time - this conflates three concerns

**②ISeasonDictionaryProvider 接口设计与 8 个引用点的重构**
- Need to consider: Heroes/Items/Traits are read-mostly, but self-learning needs write capability. Should there be ISeasonDictionaryReader + ISeasonDictionaryWriter?
- Some sites (like ReplayCommand) might be in static contexts - DI doesn't work there. Need a service locator pattern or pass via parameter.
- `FusionArbitrator` likely runs in a pipeline with a captured context - need to ensure season/mode is passed through the pipeline, not just resolved at runtime.
- Better form: combine season+mode into a `SeasonContext` value object passed through the recognition pipeline, with provider resolved once per session.

**③自学习闭环与金标流程复用**
- Current gold standard flow: review → user confirms → write correction
- New self-learning: candidate during match → confirm at review → formal inclusion
- Conflict: gold standard currently writes corrections to existing dictionary entries. Self-learning adds NEW entries. The "三合一" mixes them.
- Better: separate "correction" (update existing) from "inclusion" (add new), but unify at the UI confirmation step.

**④三重初始字典来源落地顺序**
- Order: ①自学习累积 (already exists from previous seasons) → ②一图流导入 (one-time bulk import) → ③网络搜索 (fallback for gaps)
- Network search: needs human review (人审) because auto-parsed names from web are noisy. Cache results with TTL, mark as "tentative" until confirmed in actual match.
- Implementation: a `DictionarySeeder` service with three strategies, run on first-time season activation.

**⑤最大2个风险与对策**
- Risk 1: Mode switching mid-session - user starts S18匹配, switches to S16.5恭喜发财 mid-game. Dictionary must hot-swap without losing pending candidates. Solution: pending candidates keyed by season+mode, not lost on switch.
- Risk 2: Self-learning pollution - wrong OCR recognition gets confirmed by user as "new entry" and pollutes dictionary permanently. Solution: require N≥2 independent recognitions OR explicit "new entry" toggle in review UI (not just confirm).

Let me write this concisely in ≤650 Chinese characters.

Actually, 650字 means 650 Chinese characters. Let me count carefully and be concise.

Let me draft:

---

**①方案成立,但有两处硬伤**
- 字典按 season+mode 二维切分,MetaInfo 却只按 patch 一维——同一 patch 下"匹配"与"恭喜发财"的羁绊/英雄差异(尤其 S18 狂暴)无法表达。应让 MetaInfo 也支持 mode,或显式声明"mode 只影响字典,不影响 MetaInfo"并文档化。
- "三合一"在校准时同时写金标+收录+纠错,职责耦合。纠错(改已有)与收录(加新)语义不同,失败回滚粒度不同,应拆分。

**②接口与重构**
建议 `ISeasonDictionaryProvider` 拆为读/写两接口:
- `ISeasonDictionaryReader.Get(season, mode)→SeasonDictionary`
- `ISeasonDictionaryWriter.AddCandidate/Confirm/Revoke`
8 引用点统一通过 `SeasonContext {SeasonId, Mode}` 在识别管线入口注入,沿管线透传;`ReplayCommand` 等静态入口改为接收 `SeasonContext` 参数(命令模式),不再 static 调 static。`FusionArbitrator` 等 async 服务走构造函数注入 Reader。纯算法 `TryFuzzyMatch/Levenshtein/TryParseNumeric` 保留 static,接收字典作为参数而非闭包内捕获。

**③金标闭环复用**
现有 UI-REVIEW 确认流可承载"纠错";自学习的"收录"是新增分支。两者入口同、提交事务不同:同一确认按钮,后端按"是已有纠错 vs 是新候选"分派。冲突点:用户确认新候选时,金标样本可能正是这张图——需把候选的 frame+box 一并写入金标,保证可追溯。

**④初始字典落地顺序与网络搜索**
顺序:①加载本机历史自学习 JSON → ②一图流批量导入(一次性,带版本号) → ③网络搜索兜底。
网络搜索必须人审:OCR 噪声大,自动收录会污染。建议实现 `DictionarySeeder`:搜索结果以"待审候选"落盘,UI 列表勾选确认后才进正式字典;缓存按 season 维度,TTL 至下个 patch。

**⑤两大风险与对策**
1. **中途切赛季丢候选**:用户 S18 匹配中切到 S16.5,候选未确认则丢失。对策:候选按 `(seasonId, mode)` 隔离存储,切换不删,回切可恢复。
2. **误收录污染**:OCR 错字被用户当作新英雄确认。对策:确认新候选需 ≥2 次独立识别命中,或在 UI 显式勾选"这是新条目"(区别于纠错),否则只入候选不入正式。

---

Let me count: this is roughly 600+ characters. Let me check the character count more carefully.

Actually, let me count the Chinese characters (excluding punctuation and numbers):

①方案成立,但有两处硬伤 - 14
字典按 season+mode 二维切分,MetaInfo 却只按 patch 一维——同一 patch 下"匹配"与"恭喜发财"的羁绊/英雄差异(尤其 S18 狂暴)无法表达。应让 MetaInfo 也支持 mode,或显式声明"mode 只影响字典,不影响 MetaInfo"并文档化。- ~95
"三合一"在校准时同时写金标+收录+纠错,职责耦合。纠错(改已有)与收录(加新)语义不同,失败回滚粒度不同,应拆分。- ~50

②接口与重构 - 6
建议 ISeasonDictionaryProvider 拆为读/写两接口 - 20
ISeasonDictionaryReader.Get(season, mode)→SeasonDictionary - 25
ISeasonDictionaryWriter.AddCandidate/Confirm/Revoke - 30
8 引用点统一通过 SeasonContext {SeasonId, Mode} 在识别管线入口注入,沿管线透传 - 35
ReplayCommand 等静态入口改为接收 SeasonContext 参数(命令模式),不再 static 调 static - 30
FusionArbitrator 等 async 服务走构造函数注入 Reader - 20
纯算法 TryFuzzyMatch/Levenshtein/TryParseNumeric 保留 static,接收字典作为参数而非闭包内捕获 - 45

③金标闭环复用 - 7
现有 UI-REVIEW 确认流可承载"纠错";自学习的"收录"是新增分支 - 30
两者入口同、提交事务不同:同一确认按钮,后端按"是已有纠错 vs 是新候选"分派 - 35
冲突点:用户确认新候选时,金标样本可能正是这张图——需把候选的 frame+box 一并写入金标,保证可追溯 - 45

④初始字典落地顺序与网络搜索 - 13
顺序:①加载本机历史自学习 JSON → ②一图流批量导入(一次性,带版本号) → ③网络搜索兜底 - 45
网络搜索必须人审:OCR 噪声大,自动收录会污染 - 20
建议实现 DictionarySeeder:搜索结果以"待审候选"落盘,UI 列表勾选确认后才进正式字典;缓存按 season 维度,TTL 至下个 patch - 60

⑤两大风险与对策 - 8
1. 中途切赛季丢候选:用户 S18 匹配中切到 S16.5,候选未确认则丢失。对策:候选按 (seasonId, mode) 隔离存储,切换不删,回切可恢复。 - 55
2. 误收录污染:OCR 错字被用户当作新英雄确认。对策:确认新候选需 ≥2 次独立识别命中,或在 UI 显式勾选"这是新条目"(区别于纠错),否则只入候选不入正式。 - 65

Total roughly: 14+95+50+6+20+25+30+35+30+20+45+7+30+35+45+13+45+20+60+8+55+65 = ~753

That's over 650. Need to trim.

Let me cut some parts:

Actually, the limit is 650字. Let me be more concise:

---

**①方案基本成立,两处硬伤**
- 字典按 season+mode 二维切,MetaInfo 仅 patch 一维——同 patch 下"匹配"与"恭喜发财"差异(S18 狂暴尤其)无处安放。需让 MetaInfo 也支持 mode,或文档化声明 mode 不影响 MetaInfo。
- "三合一"(金标+收录+纠错)耦合过紧:纠错(改已有)与收录(加新)语义/回滚粒度不同,应拆为两个事务。

**②接口与重构**
`ISeasonDictionaryProvider` 拆读写两接口:
- `Reader.Get(season,mode)→SeasonDictionary`
- `Writer.AddCandidate/Confirm/Revoke`

8 引用点统一通过 `SeasonContext{SeasonId,Mode}` 在识别管线入口注入并沿管线透传。`ReplayCommand` 等静态入口改为接收 `SeasonContext` 参数的命令模式,不再 static 调 static;`FusionArbitrator` 等 async 服务构造函数注入 Reader。纯算法 `TryFuzzyMatch/Levenshtein/TryParseNumeric` 保留 static,但字典作为参数传入而非闭包捕获。

**③金标闭环复用**
UI-REVIEW 现有确认按钮同时承载纠错与收录,后端按"已有纠错 vs 新候选"分派事务。冲突点:确认新候选时,金标样本恰是候选来源帧——需把候选 frame+box 一起写入金标,保证可追溯。

**④初始字典落地**
顺序:①加载本机历史自学习 JSON → ②一图流批量导入(带版本号) → ③网络搜索兜底。
网络搜索**必须人审**:OCR 噪声大,自动收录会污染。实现 `DictionarySeeder`,搜索结果以"待审候选"落盘,UI 勾选确认才入正式字典;缓存按 season 维度,TTL 至下个 patch。

**⑤两大风险与对策**
1. **中途切赛季丢候选**:候选按 `(seasonId,mode)` 隔离存储,切换不删,回切可恢复。
2. **误收录污染**:确认新候选需 ≥2 次独立识别命中,或 UI 显式勾选"这是新条目"(区别于纠错),否则只入候选不入正式。

---

Let me count again more carefully. Chinese characters only:

①方案基本成立,两处硬伤 (12)
字典按 season+mode 二维切,MetaInfo 仅 patch 一维——同 patch 下匹配与恭喜发财差异(S18 狂暴尤其)无处安放。需让 MetaInfo 也支持 mode,或文档化声明 mode 不影响 MetaInfo。(~70)
三合一(金标+收录+纠

---

## Qwen 3.8 Flash (风险)

①方案成立，但最大缺陷是缺“Season/Mode/Patch/字典快照”的权威映射。S18匹配/狂暴、S16.5恭喜发财如何对应目录、版本、MetaInfo、prompt，未定义；候选、别名、改名、prompt膨胀也未治理。

②Provider可行，但不能暴露可写集合。建议：`ISeasonDictionaryProvider { string SeasonId; string Mode; SeasonDictionarySnapshot GetSnapshot(); bool TryGetHero(...); IReadOnlyList<Hero> GetHeroes(); }`。纯算法仍static，但输入snapshot。ViewModel/服务注入provider；`ReplayCommand`等静态调用改显式传provider或`SeasonRuntime.Current`。用feature flag双读旧static与新provider，日志差异，灰度切换。

③可复用Review确认事务，但需新增“未收录候选”面板。实时只写pending，不进正式prompt；回顾确认=金标+收录+纠错原子操作，失败回滚。冲突在于金标偏帧级，收录偏词表级，必须带候选ID、来源帧、置信度、别名和撤回能力。

④落地顺序：先拆Provider+内置seed字典；再上自学习候选；再一图流导入（OCR+人工映射）；网络搜索最后。网络搜索不得实时进识别，应离线生成candidate包，含source/url/time/confidence，缓存+人审或抽样审后合并。

⑤最大风险：1 VLM幻觉被确认污染字典：多源交叉、多帧一致、低置信只候选、收录需证据、可回滚、prompt裁剪。2 跨赛季串扰与重构回归：不可变snapshot、SeasonModeRegistry、8引用点双读校验、集成测试。

---
