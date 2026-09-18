# UI 桌面窗口 + 边运行边标注 · 委员会审议 (Live)

> 日期: 2026-09-16 ｜ 模型: DeepSeek V4.1 Flash / MiniMax M3 / Qwen 3.8 Flash

## DeepSeek V4.1 Flash (需求)

①方向成立，但草案把“边运行边标注”当低机制。最大遗漏：缺运行时低摩擦交互、帧留存/可重放、版本上下文、修正操作语义与评测定义。信息层级应“推荐主、识别辅、修正折叠”；快捷键、一键确认、异常优先。手机端预留平台无关 VM、区域坐标、输入/窗口形态抽象。

②CorrectionRecord 不完备。需加 MatchId、Round、RecognizerVersion、FrameId/Hash、Cell 候选、错误类型（漏检/误检/定位错/分类错）、批量/整帧、确认状态、schemaVersion、CausationId；RecognizedValue 必须可 null 表示漏检。准确率还需未修正项与抽样审计，否则只测“纠错率”，不测召回，不能真正补 P0 金标。

③语义应双层：原始 RecognitionFrame 不可变；CorrectionOverlay→EffectiveSnapshot。运行时决策读 Effective，并 DecisionRevision++ 重算当前状态；历史复盘/评测读 Raw。历史决策不重写，重算防抖或手动触发，避免每键刷新。

④UI 不硬依赖 MetaInfo。P0：识别显示+修正+标注；P1：接现有 TacticalAdvisor 推荐；P2：CompCode v5 定稿后，用 ICompCodeProvider 接阵容码复制，未定稿先占位禁用。

⑤应进 MatchLogger。作为 CorrectionApplied 事件 append-only，带 schemaVersion/Sequence/CausationId，勿另开孤立 JSONL。复盘做双视图：as-seen 与 corrected-GT；R0/R2 默认 as-seen，评测用 corrected，避免人工标注污染当时决策评分。

⑥风险一：选择偏差/噪声——用户只改记得的格，数据非随机。对策：分层置信度、记录未修正项、抽样复核、区分自动 GT/人工 GT。风险二：快节奏下修正负担导致漏标。对策：异常优先、快捷键、一键全对、赛后补标、非阻塞；手机端预留触控与悬浮窗抽象。

---

## MiniMax M3 (架构)

The user is asking me to act as a member of the "铲瞳 ChanSight" Requirements and Design Committee. I need to critique a UI desktop window + "label while running" proposal. The response should be ≤650 characters (Chinese characters likely, given the context), and should address 6 specific points.

Let me think carefully about each point:

① 该 UI 需求与规划的成立性、最大遗漏
- The need is valid: independent desktop window for portability
- Big omissions: 
  - No mention of capture source for the desktop window (does it own a capture loop, or does Recorder feed it? If Recorder feeds it, what's the IPC/file mechanism?)
  - No mention of frame timing/synchronization - if UI is on a different thread/process from recognition, how to keep them in sync?
  - No mention of multi-monitor / DPI / game window positioning (TFT is a game, the user is playing it)
  - No mention of state when game is not focused / minimized
  - "Future mobile floating window" - this is a huge architectural constraint that should drive the design NOW (e.g., platform-agnostic rendering, no Win32-only APIs)
  - Missing: what happens when recognition fails / partial recognition? UI needs a "manual override" mode
  - Missing: permissions (screen capture permission on macOS, etc.)
  - Missing: latency budget - if user is playing TFT, recommendations need to be < 1s

② CorrectionRecord 完备性
- Fields listed: Id/Timestamp/SourceFrame/RegionType/CellIndex/RecognizedValue/CorrectedValue/Confidence/SourceTier
- Missing fields for "tuning accuracy":
  - **RegionType 枚举需要细化**: 28 board cells, 9 bench, 5 shop, gold, level, stage, HP - need a discriminator, not just "RegionType"
  - **CellIndex schema**: 28 cells needs row/col encoding, not just index
  - **RecognizedValue / CorrectedValue**: what's the type? string? int? enum? Needs a discriminated union or tagged value
  - **SourceTier**: from the three-tier upgrade chain - but which tier won? Need all three tier outputs, not just the winner
  - **All three tier outputs**: For accuracy analysis, you need what V0 said, what VLM said, what the local said - not just the final fused result
  - **Frame hash / image crop path**: To re-verify against the original frame
  - **User identity / session ID**: For multi-user scenarios
  - **Correction reason / category**: Why was it wrong? (occluded, wrong template, lighting, etc.) - critical for tuning
  - **Game state context**: What was the stage/round when correction happened? Some errors are stage-specific
  - **Schema versioning**: For long-term dataset
- For "tuning accuracy" specifically, you need:
  - Ground truth (corrected value)
  - All model predictions (not just fused)
  - Frame reference
  - Context (stage, etc.)
  - This is essentially a COCO-style annotation format

③ 修正后回写 GameStateSnapshot 影响决策重算
- This is a semantic question: should corrections trigger re-decision?
- YES, but with caveats:
  - Need debouncing (user might correct multiple cells in quick succession)
  - Need to mark the snapshot as "user-corrected" so downstream knows it's not raw recognition
  - Need to NOT pollute MatchLogger with intermediate states - only log the final state per round
  - Risk: if user corrects mid-round, the decision changes - but the round is already in progress. Need to clarify: corrections are advisory for the CURRENT decision, but the canonical record is what was actually played
  - Also: corrections should be reversible (undo)
  - And: the engine should distinguish "live correction" (affects current decision) from "post-hoc correction" (only affects dataset)

④ UI 与 MetaInfo 依赖顺序
- MetaInfo v5 (CompCode) is undefined
- UI shouldn't block on CompCode - the recommendation card can show CompName first, CompCode as a "copy" button later
- Phased approach:
  - Phase 1: UI ships with placeholder CompCode (just the internal ID), copy works
  - Phase 2: When MetaInfo v5 lands, swap in the real code
  - The contract between UI and MetaInfo should be: `ICompDefinition { Code: string; Name: string; ... }` - the UI doesn't care what format Code is in, as long as it's a string
  - Risk: if MetaInfo v5 changes the code format later, all copied codes become invalid - need a version field

⑤ 标注数据是否进 MatchLogger
- This is critical for R0/R2 consistency
- Currently MatchLogger is append-only JSONL of game events
- Corrections are NOT game events - they're meta-events about the recognition pipeline
- Options:
  - A: Add a new event type `CorrectionEvent` to MatchLogger - YES, this is the right answer
  - B: Separate CorrectionLog file - cleaner separation but harder to correlate
  - Recommend A with a discriminator field
- For R0 (scoring): correction events should NOT affect the score (they're not gameplay)
- For R2 (LLM report): corrections should be highlighted as "recognition was wrong here" - useful context
- Need to ensure: correction events have stable IDs that can be cross-referenced with the snapshot they corrected

⑥ 最大的 2 个风险与对策
Risk 1: **Capture-to-UI pipeline latency / threading**
- If the desktop UI process is separate from the recognition process, IPC adds latency
- If same process, UI thread blocks recognition
- Mitigation: shared memory ring buffer for frames, UI subscribes via observable, recognition runs on dedicated thread with priority
- Also: capture may need to run in a service/background, not the UI process (especially for mobile floating window future)

Risk 2: **"边运行边标注" dataset quality / bias**
- User only corrects what they notice is wrong - silent errors are never labeled
- This creates a biased dataset (only obvious errors)
- Mitigation: 
  - Periodic "spot check" mode where UI shows random frames and asks "is this correct?"
  - Track correction rate per RegionType/CellIndex - if a region has 0 corrections, that doesn't mean it's accurate
  - Active learning: prioritize showing uncertain recognitions for verification

Other risks worth mentioning:
- **Correction race condition**: user corrects while recognition updates the snapshot - need locking or version vectors
- **Privacy/security**: screen capture of a game is fine, but if extended to other apps, becomes a keylogger

Let me now structure this into ≤650 Chinese characters. That's tight. Let me draft:

---

① 成立性: 桌面窗口 + 移动端悬浮窗的"未来可移植"是伪需求驱动真约束——意味着渲染/输入/平台抽象必须现在就做(IAvaloniaHeadless + Skia 渲染层剥离),草案未提。最大遗漏: 捕获源归属未明(UI 自捕获 vs Recorder 喂帧,跨进程 IPC/共享内存未设计)、识别失败/部分识别的降级 UI 无规划、权限与延迟预算(<1s 推荐)未约束。

② CorrectionRecord 不完备,撑不起调参:
- 缺三路原始输出(V0/VLM/本地),仅记融合后值无法定位是哪层错;
- RecognizedValue/CorrectedValue 类型未定义(string/int/enum 需可辨识联合);
- CellIndex 缺行列编码(28格用纯索引难可视化);
- 缺 GameState 上下文(阶段/回合)、缺修正原因分类(遮挡/模板/光照);
- 缺帧图裁剪引用或哈希(无法回溯原图);
- 缺 SchemaVersion。

③ 语义必须区分:
- 实时修正(影响本回合决策)需 debounce + 快照标记"user-corrected",且不污染 MatchLogger 中间态;
- 事后修正(仅入数据集)走另一通道;
- 决策重算应是"建议刷新"而非"自动执行",用户需确认。

④ UI 不应阻塞 MetaInfo v5。契约先行: `ICompDefinition { Code:string; Name:string; Version:int }`,UI 只认字符串。分期: P1 内部 ID 占位 + 复制可用,P2 v5 落地替换。Code 格式变更需 Version 字段兜底,旧码可解析。

⑤ 必须进 MatchLogger,作为新事件类型 `CorrectionEvent`,带 discriminator 与 snapshotId 关联。R0 评分不计,R2 报告标注"识别错误点"作为上下文。

⑥ 两大风险:
1. 标注偏差——用户只改看得见的错,静默错误零标签,数据集系统性偏。需主动学习: 低置信度帧优先弹窗核验, 按 RegionType 统计修正率而非假设零修正=零错误。
2. 修正竞态——用户改到一半识别线程已更新快照。需版本向量或乐观锁,UI 持有本地草稿,提交时校验 snapshot version,不匹配则提示"识别已更新,请复核"。

---

Let me count characters... this is roughly 700+ characters. Need to trim.

Let me trim more aggressively:

① 成立但有真约束: "未来可移植手机端"必须现在就驱动平台抽象(Skia 渲染层剥离、IAvaloniaHeadless)。最大遗漏: 捕获源归属未明(UI 自捕获 vs Recorder 喂帧,IPC/共享内存未设计)、识别失败降级 UI 无规划、推荐延迟预算<1s 未约束。

② CorrectionRecord 不完备: 缺三路原始输出(V0/VLM/本地),仅记融合值无法定位错层; Value 类型未定义(需可辨识联合); CellIndex 缺行列编码; 缺 GameState 上下文(阶段/回合)、修正原因分类(遮挡/模板/光照)、帧图裁剪引用/哈希、SchemaVersion。撑不起调参。

③ 须区分两类修正语义: 实时修正(影响本回合决策)需 debounce + 快照标"user-corrected",不污染 MatchLogger 中间态; 事后修正仅入数据集。决策重算应是"建议刷新"非"自动执行",用户确认。

④ UI 不阻塞 MetaInfo v5。契约先行 `ICompDefinition { Code:string; Name:string; Version:int }`,UI 只认字符串。P1 内部 ID 占位+复制可用,P2 v5 替换。Code 格式变更靠 Version 兜底。

⑤ 必须进 MatchLogger,新增 `CorrectionEvent` 类型,带 snapshotId 关联。R0 评分不计,R2 报告标注"识别错误点"。

⑥ 两大风险:
1. 标注偏差——用户只改可见错,静默错零标签,数据集系统性偏。需主动学习: 低置信度帧优先弹窗核验,按 RegionType 统计修正率而非假设零修正=零错误。
2. 修正竞态——用户改到一半识别线程已更新快照。需版本向量/乐观锁,UI 持本地草稿,提交时校验 snapshot version,不匹配则提示"识别已更新,请复核"。

Let me count more carefully... this should be around 600 characters. Good.

Actually, let me reconsider some technical points:

For ③: The decision recompute semantic - I want to be more precise. The key question is: when user corrects, does the engine re-decide? And the answer is nuanced:
- If correction is during the round (real-time), it should refresh recommendation but not auto-apply
- If correction is between rounds, it should affect next decision
- The snapshot itself should be marked as "corrected" so downstream consumers know
- MatchLogger should only see the canonical (final) state per round, not the intermediate corrections

For ⑥ risk 2 (race condition): This is a real concern. In Avalonia MVVM, the UI binds to observable properties. If recognition updates the snapshot every 200ms while user is typing, the user's input gets blown away. Need either:
- Edit-in-place mode that locks the cell
- Version vector on snapshot
- Local draft state in

---

## Qwen 3.8 Flash (风险)

①成立：独立窗口必要，但最大遗漏是“实时识别—用户修正—决策重算—标注”的一致性契约。未定义状态冲突、版本、防抖、可观测与性能预算；MVVM 也会放大绑定复杂度。

②CorrectionRecord 不完备，不能直接当金标。必须补 matchId/frameId/snapshotId、pipeline/model版本、候选值与概率、bbox/region、correctionType、是否确认、撤销链、关联决策。建议分 soft correction 与 gold label；只有用户确认且通过质量门才进评测集。测准确率要可重放同帧同版本。

③修正不应直接覆盖原始 GameStateSnapshot。应保留原始识别事实，生成 WorkingSnapshot/CorrectedView。决策默认用仲裁后状态；用户修正触发重算需 debounce、最小间隔、优先级，避免抖动。

④UI 与 MetaInfo 解耦分期：P0 做窗口、识别展示、修正标注、推荐；P1 做阵容码复制，待 CompCode schema冻结。UI 可依赖 ICompCodeCodec 接口，未定稿则隐藏/禁用，避免空转。

⑤标注应进 MatchLogger 事件流，新增 CorrectionRecorded 事件，记录引用/hash，不塞图片。R0/R2 才能回放“识别→修正→决策”。

⑥风险1：用户乱点污染金标。对策：显式“确认标注”、质量门、冲突检测、采样复核、数据集版本化。风险2：高频刷新拖垮 UI/决策。对策：不可变快照、diff 绑定、背压/节流、异步落盘、性能预算与降级。

---
