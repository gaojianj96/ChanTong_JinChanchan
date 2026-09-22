# 识别与推荐架构演进 · 委员会审议 (Live)

> 日期: 2026-09-17 ｜ 模型: DeepSeek V4.1 Flash / MiniMax M3 / Qwen 3.8 Flash

## DeepSeek V4.1 Flash (需求)

①方向成立，但当前定义过粗。最大缺陷：把“手动整帧 VLM”当兜底，却未定义字段级置信度、来源标记、可回滚与修正闭环，极易变成“手动录入”。遗漏：敌我视角/对手序号/回合元数据未进契约，回放重模拟会混帧；自动 2fps 无标签，OCR 空桩下短期训练闭环不成立。

②输入契约：不建议裸送 4K 整帧。应“整帧缩略图定位 + ROI 原图裁剪”多图输入：玩家条、棋盘、经济/血量、装备栏。输出固定 JSON：frame_id、perspective(self/opponent)、opponent_index/name/hp/gold/level/traits、board[{cell,unit,star,items,conf}]、stage/round、source=manual_vlm、prompt_version。对手序号必须输出候选列表+选中序号+置信度，UI 用玩家条高亮/头像/座位顺序让用户确认。一次输出全字段可，但 UI 按字段分组，低置信黄标，允许部分采纳，禁止静默覆盖旧值。

③双轨推荐：算法推荐自动实时，LLM 推荐仅手动触发。同屏并列，标签、时间戳、依据、置信度、采纳/忽略。LLM 只读“已确认数据”，数据版本变化后标“已过期”，不自动重跑；算法可自动刷新。二者独立折叠，不互相覆盖。

④回放重模拟：数据源用关键帧+事件帧，不逐全帧 VLM。链路：FrameArchive→已有识别直读→缺失/用户点选帧才调 VLM→重模拟→双轨推荐。VLM 成本控制：frame_hash+prompt_version 缓存、队列限流、单帧手动、批量仅用户标记帧。

⑤需求视角最大风险：1）手动兜底变手动录入，操作负担高；对策：字段级确认、默认保留旧值、快捷键、批量修正、模板。2）对手序号/敌我混淆导致推荐错；对策：强制视角元数据、玩家条锚点、用户确认，回放显式切换己方/对手。对手信息展示“序号+玩家名+血量+经济+棋子/装备”基本够，但应补等级、羁绊、核心装备、存活状态，否则 LLM 推荐依据不足。

---

## MiniMax M3 (架构)

The user wants me to act as a member of the "铲瞳 ChanSight" requirements and design committee, reviewing an architecture evolution proposal. They want me to be critical, not sycophantic, and provide specific solutions. Let me analyze each point carefully.

Key facts:
- OCR is a stub returning empty
- 4K resolution, ROI math correct but not validated on real 4K
- VLM piece recognition has errors (variable cells packaged together, localization/semantic errors)
- Existing infrastructure: FrameArchive, VideoRecorder/DatasetSampler (2fps), RecognitionPipeline, TacticalAdvisor, LLMAdvisor, DecisionPanelService, AnnotationStore

User decisions:
1. Manual + auto recognition coexist; manual is fallback
2. Manual = full-frame VLM trigger, identifies everything at once
3. Auto 2fps recording kept as training data
4. Dual recommendation: algorithm (auto) + LLM (manual), both displayed with labels
5. User can manually edit all data
6. Replay re-simulation using recorded frames

User supplementary:
A. Manual full-frame VLM as fallback this round; auto stays broken
B. Full-frame VLM should identify opponent number; UI shows index + name + HP + economy + board
C. Replay: use existing recognition results, don't auto re-VLM; user can manually trigger VLM on specific frames

My view: Architecture perspective
- Input/output contract for manual full-frame VLM (4K token cost vs precision, whether to crop board area)
- How opponent index recognition lands
- Dual recommendation dependencies and triggers
- Replay re-simulation: reuse FrameArchive or VideoRecorder
- Interface with existing RecognitionPipeline/TacticalAdvisor/LLMAdvisor

Need to output ≤650 characters (Chinese), with 5 sections:
① Does this architecture direction hold? Biggest flaw/omission
② Manual full-frame VLM input/output contract design (full 4K vs crop board area; one-shot output stability; opponent index recognition)
③ Dual recommendation (algorithm vs LLM) display and trigger semantics
④ Replay re-simulation data source (keyframes vs all frames) and link range, VLM cost control
⑤ Top 2 risks from my view and countermeasures

Let me think hard about each:

① Direction assessment:
- Direction is reasonable as transitional architecture, but there are real flaws:
  - **Biggest flaw**: "manual full-frame VLM as fallback" creates a hybrid system where auto recognition is knowingly broken but still running, wasting compute and polluting FrameArchive with bad data. This is technical debt accumulation.
  - Missing: how does manual VLM result flow back into the same data model that auto recognition writes to? If they have different schemas, downstream consumers (TacticalAdvisor) will break.
  - Missing: the manual VLM contract must be the **canonical schema** going forward, not a side channel. Otherwise when we fix OCR/auto later, we'll have schema migration pain.

② Manual full-frame VLM contract:
- 4K full frame: ~3840×2160 = 8.3M pixels. With typical VLM downsampling to 1024×1024 or similar, that's manageable but loses detail for small chess pieces. Token cost is moderate but quality suffers.
- Crop board area: better precision, lower token cost, but requires knowing board location first (chicken-and-egg if auto recognition is broken).
- **My recommendation**: Hybrid — provide both. Default to a **fixed template crop** based on known UI layout (top bar + opponent board area), but allow user to draw custom ROI. The crop should be ~1280×720 or similar.
- One-shot full-field output: VLM JSON schema with strict fields. Need validation layer (schema check + range check + cross-field consistency).
- Opponent index: This is hard because the "number" (1-8) is a UI element. Need to either:
  - Crop the opponent header area specifically
  - Or have VLM identify by position + name + level
- **Stability**: Use structured output (JSON mode), validate against schema, flag low-confidence fields, allow user to edit.

③ Dual recommendation:
- Algorithm (TacticalAdvisor): auto-triggered when data updates, real-time. Already exists.
- LLM (LLMAdvisor): manual trigger, user clicks button. Already exists.
- Display: side-by-side or stacked, clearly labeled "算法推荐" vs "LLM 推荐"
- **Issue**: If auto recognition is broken, algorithm recommendation is based on garbage data. This is a hidden coupling. Need to show "data quality" indicator.
- **Trigger semantics**: algorithm runs on every data update; LLM runs on explicit user click. Both should be cached with timestamps.
- **Missing**: when data is manually edited, should algorithm re-run? Yes, but with debouncing.

④ Replay re-simulation:
- Data source: FrameArchive (keyframes) for quick navigation; VideoRecorder (2fps) for smooth playback.
- **My recommendation**: Use FrameArchive as the primary timeline (matches the "key frame" concept), but load adjacent frames from VideoRecorder for smooth playback between keyframes.
- Link range: recognition results → tactical state → recommendation. All derived from frame data, so replay should re-derive (or read cached) at each frame.
- VLM cost: per user decision C, don't auto-trigger VLM in replay. Only manual trigger. This means replay is "read-only" for recognition, but user can trigger VLM on specific frames.
- **Missing**: replay needs to show which frames have recognition results vs which don't. Visual indicator.

⑤ Top 2 risks:
- Risk 1: **Schema divergence** — manual VLM and auto OCR/VLM produce different field names/granularities. Downstream breaks. Counter: define a unified `GameStateSnapshot` schema now; both paths write to it; AnnotationStore validates.
- Risk 2: **Data quality blindness** — algorithm recommendation runs on broken auto data, user trusts it. Counter: add data quality score (e.g., "auto recognition confidence: low"), show warning when auto data is stale/broken, require manual VLM before showing algorithm recommendation in this transitional phase.

Let me also think about:
- The "manual full-frame VLM" as fallback means user has to manually trigger every time they need accurate data. This is a UX burden. But user has accepted this.
- Token cost for 4K: if using GPT-4V or similar, 4K image is ~85 tokens at low res, or ~1100+ at high res. With JSON output, total ~2000-3000 tokens per call. At 2fps, if user triggers every few seconds, cost is manageable.
- But wait — user said manual is only triggered when auto fails. So cost is bounded by user patience, not by frame rate. Good.

Let me write the response in Chinese, ≤650 characters. Need to be tight.

Let me draft:

①方向成立但有重大遗漏: 手动VLM与自动识别**没有统一数据契约**, 下游(TacticalAdvisor)会读到双源不一致的脏数据; 且"自动维持现状(错就错)"会让FrameArchive持续积累错误金标候选, 污染未来训练。建议本轮就冻结GameStateSnapshot统一schema, 手动VLM作为权威写入, 自动结果仅作"快速预览+待修正"标记。

②契约设计:
- 输入: **不做整帧4K**(token贵且小棋粒丢失), 改为**两段固定模板裁剪**——对手信息条(顶部~1280×120)+ 棋盘区域(根据对手序号动态定位, 默认8×8格, 约1920×1080); 允许用户覆盖ROI。
- 输出: **单一严格JSON**, 字段: opponent_index, player_name, hp, gold/level, board[][{star, item[], pos}], bench[]; 配套schema校验+字段置信度; 低置信字段强制UI标红待修。
- 对手序号: VLM不直接读"几", 而读**信息条位置+玩家名+等级组合**, 后端用(player_name, level)去重映射序号; 序号显示由UI根据当前轮次推断。

③双轨推荐:
- 算法: 数据更新即跑(去抖300ms), 实时; 但**当数据源含"自动未修正"标记时, 推荐卡片顶部显示"⚠ 数据未校准"**。
- LLM: 手动点击触发, 缓存最近3条; 显式标注「算法推荐」「LLM推荐」+ 时间戳+ 数据快照ID, 便于回溯。
- 手动编辑数据后: 算法自动重跑, LLM不自动跑(避免token浪费)。

④回放重模拟:
- 数据源: **FrameArchive做时间轴骨架**(关键帧对齐回合节点), VideoRecorder 2fps做帧间插值; 识别结果按frame_id挂载, 命中即直读, 未命中显示"未识别"占位。
- 链路范围: 帧 → 快照(直读) → 战术状态 → 算法推荐(重算); LLM推荐**不重算**(成本+无新数据)。
- VLM成本: 回放中**禁止自动VLM**, 仅用户右键单帧触发; 触发走手动VLM同一管线, 结果写回AnnotationStore并刷新该帧快照。

⑤最大风险:
- **风险1: 双源schema漂移** → 对策: 本轮定义GameStateSnapshot为唯一权威, 手动VLM输出经校验后写入; 自动路径输出标注deprecated, 不入主表。
- **风险2: 算法推荐建立在脏数据上, 用户误信** → 对策: 引入数据健康度评分(自动识别覆盖率+最近一次手动校准时间), <阈值时推荐卡片强制置灰并提示"请先手动校准本帧"。

Let me count characters... this is going to be over 650. Need to compress.

Let me rewrite tighter:

①方向成立, 两大遗漏:
- 手动VLM与自动识别**无统一数据契约**, 下游读到双源脏数据会崩;
- "自动维持错"会让FrameArchive持续污染未来训练集。
对策: 本轮冻结GameStateSnapshot统一schema, 手动VLM为权威写入, 自动结果标"待修正"。

②契约:
- 输入**不取整帧4K**(token贵+小棋粒糊), 改**对手信息条(~1280×120)+ 棋盘区(动态定位, 默认~1920×1080)**两段裁剪, 允许用户覆盖ROI。
- 输出**严格JSON**: opponent_index(由player_name+level反查)、player_name、hp、gold/level、board[][{star,item[],pos}]、bench[]; 配schema校验+字段置信度, 低置信UI标红。
- 对手序号: VLM不直读数字, 读**信息条位置+名+等级**, 后端映射。

③双轨:
- 算法: 数据更新去抖300ms实时跑; 但**数据含"未校准"标记时卡片顶部⚠提示**。
- LLM: 手动触发, 缓存3条; 显式标注「算法/LLM」+时间戳+快照ID。
- 手动编辑后: 算法重跑, LLM不跑(省token)。

④回放:
- 源: **FrameArchive做骨架**(对齐回合), VideoRecorder 2fps做帧间插值; 快照按frame_id挂载, 命中直读, 未命中占位。
- 链路: 帧→快照(直读)→战术态→算法推荐(重算); LLM**不重算**。
- VLM: 回放中**禁自动**, 仅右键单帧触发, 走同一管线写回AnnotationStore。

⑤风险:
- **schema漂移** → 冻结GameStateSnapshot, 手动VLM为权威, 自动路径标deprecated不入主表。
- **算法推荐基于脏数据被误信** → 引入数据健康度分(自动覆盖率+最近校准时间), 低于阈值推荐置灰+强制"先校准本帧"。

Let me count this more carefully. The Chinese character count is roughly... let me estimate. Each section is about 100-150

---

## Qwen 3.8 Flash (风险)

①成立但不该把“手动整帧VLM”当主兜底；最大缺陷：缺帧身份/视角归属、置信度、修正回流，且会让自动长期停滞。方案：手动=标注/确认模式，自动维持现状但冻结，推荐只读已确认数据。

②输入勿整帧4K直喂：裁棋盘ROI+HUD小图（阶段/血量/经济/玩家面板），对手序号从玩家面板/血条证据识别。输出JSON：frame_id, view_type, opponent_index, player_name, stage,hp,gold,board[{x,y,piece,level,items,conf}], unknown_fields。一次全字段不稳：强约束“无证据则null”，对手序号必须UI显式，否则unknown；可拆两次调用（结构/棋盘，装备/对手）。

③算法：状态更新后实时，标签“算法推荐”，可自动刷新；LLM：仅手动触发，标签“LLM推荐”，记录输入版本/时间，不自动执行。冲突并列显示，不合并。

④回放以关键帧/变化帧为主，全帧仅低清抽样；链路：录帧→已有识别/用户修正→状态重放→算法。VLM默认不调，仅用户点某帧或关键帧缺失时调用，且ROI降采样+缓存。

⑤风险1：4K小棋子/装备识别差、成本高。对策：ROI/降采样/分字段/置信度/人工修正，未确认不推荐。风险2：手动兜底长期化污染训练与技术债。对策：自动结果隔离区，金标集+回归阈值，OCR/校准达标前不得自动启用。

---
