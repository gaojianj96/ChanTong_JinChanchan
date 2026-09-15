# Retro Review E2/R1/V3 - Moonshot Kimi K3

# 回溯批量盲审报告

---

## 任务 E2 — 状态机

**判定: Pass-with-Notes**

| 级别 | 问题 |
|------|------|
| **Minor** | `GameStateManager.Update()` 与 `UpdateOpponent()` 中 `_current` 的读取-替换非原子(无 CAS/lock),并发写入时可能丢版本号或产生撕裂。当前仅测试了并发读,未覆盖并发写。 |
| **Minor** | `RecognitionToGameStateAdapter.Apply()` 每次全量重建 `GameStateSnapshot`,但 `Streak` 和 `Phase` 直接沿用旧值,若识别帧包含 Streak 信息会被静默丢弃。 |
| **Info** | `GamePhase` 转换表硬编码,后续新增阶段需同步修改字典和测试,建议补充文档注释说明设计意图。 |

---

## 任务 R1 — 回放加载

**判定: Pass**

| 级别 | 问题 |
|------|------|
| **Info** | `ReplayLoader` 无参构造函数 `public ReplayLoader() : this(new FileSystem())` 与 DI 注册并存,DI 容器走 `internal` 构造函数,设计合理但建议标注 `[ActivatorUtilitiesConstructor]` 或注释明确意图。 |
| **Info** | `ValidateTimeline` 要求 Seq 必须从 1 开始且严格连续,若上游 `MatchLogger` 因崩溃丢事件会产生 gap,回放侧仅报告不修复,属设计决策,可接受。 |

---

## 任务 V3 — VLM 适配器

**判定: Fail**

| 级别 | 问题 |
|------|------|
| **Critical** | `OpenRouterVlmClient` 中 `DefaultModel = "deepseek-v4-flash-vision-exp"` 为虚构模型名,OpenRouter 平台不存在此模型,运行时必然 404。需替换为真实可用的视觉模型标识。 |
| **Major** | `VlmRecognitionAdapter.ParseVerdicts()` 中 `GameSeasonDictionary.TryFuzzyMatch` 和 `GameSeasonDictionary.Heroes` 被引用但未在本次改动文件中定义,若该类不存在于已有代码库则编译失败;若存在,需确认其 `Heroes` 列表与当前赛季匹配,否则模糊匹配会产生错误纠正。 |
| **Major** | `OpenRouterVlmClient.CompleteAsync` 的 `catch (TaskCanceledException)` 在 `catch (OperationCanceledException) when (ct.IsCancellationRequested)` 之后,但 `TaskCanceledException` 继承自 `OperationCanceledException`,当超时触发时 `ct.IsCancellationRequested` 为 `false`,会正确落入 `TaskCanceledException` 分支——逻辑可行,但 `HttpClient.SendAsync` 超时实际抛的是 `TaskCanceledException`(非 `OperationCanceledException` 子类语义),在 .NET 6+ 中 `HttpClient` 超时抛 `TaskCanceledException`,此处处理正确,不过建议添加集成测试验证超时路径。 |

---

**总结:** E2 和 R1 质量良好可合并;V3 存在虚构模型名(Critical)和潜在编译依赖问题(Major),需修复后重新评审。