# Batch Review R0/E4/V0/D02 - Moonshot Kimi K3

# 批量独立 Full Review: R0/E4/V0/D02

---

## R0 — MatchLogger.cs

**结论: PASS-with-notes**

| # | 级别 | 问题 |
|---|------|------|
| R0-1 | **Major** | `FlushGameAsync` 执行 read-modify-write (`ReadAllText` → 拼接 → `WriteAllText`)。若进程在 write 中途崩溃，已持久化的旧数据被截断/覆盖，丢失整段历史事件。应改为 `File.AppendAllTextAsync` 或先写临时文件再原子 rename。 |
| R0-2 | **Minor** | `GetOrLoadAsync` 中 `ReadAllTextAsync` 对不存在的文件会抛 `FileNotFoundException`，首次启动（无历史文件）时 `AppendAsync` 必然异常。需检查 `FileExists` 或捕获 `FileNotFoundException` 并视为空内容。 |
| R0-3 | **Minor** | `ReadSeq` 中 `TryGetProperty("Seq", …)` 硬编码 PascalCase，但 `JsonSerializerOptions` 未设 `PropertyNamingPolicy`，序列化输出取决于 `MatchEvent` 属性名。若属性名非 `Seq`（如 camelCase），回放时 MaxSeq 永远为 0，去重失效。建议显式指定 naming policy 或用 `[JsonPropertyName]`。 |
| R0-4 | **Nit** | `FlushAsync` 遍历 `_logs` 字典时若在 `FlushGameAsync` 内部抛出异常，后续 gameId 的 pending 数据不会被 flush，且无部分成功日志。 |

---

## E4 — CompKnowledgeBase.cs

**结论: PASS**

| # | 级别 | 问题 |
|---|------|------|
| E4-1 | **Nit** | `QueryByUnits` 排序仅按 hits 降序，同 hits 时顺序不稳定（依赖 `_templates` 原始顺序）。若需确定性输出，可加 `.ThenBy(x => x.Template.Id)`。 |
| E4-2 | **Nit** | `Load` 方法非线程安全（直接替换 `_templates` 引用），但当前设计为启动时单次加载，可接受。 |

---

## V0 — Schema Validator + 契约文档

**结论: PASS-with-notes**

| # | 级别 | 问题 |
|---|------|------|
| V0-1 | **Major** | **文档与代码字段名不一致**：文档 §3 定义 `xp`，§6 表格用 `exp`；代码校验器检查 `frame.Exp`。同一字段两个名字，V1-V5 实现者会困惑。需统一为 `exp` 或 `xp` 并同步文档 §3。 |
| V0-2 | **Major** | **文档 §6 声明 `sourceTier` 字段但代码未校验**。`boardCells`/`benchCells`/`shopCards` 每项应含 `sourceTier ∈ {T0,T1,T2,T3}`，validator 完全未检查此字段。若上游传入非法 tier，静默通过。 |
| V0-3 | **Minor** | **文档 §6 声明 `correctionFlag` / `timestamp` 但代码未校验**。`timestamp` 应验证 ISO 8601 格式，`correctionFlag` 至少确认字段存在。当前 validator 对这两个字段零覆盖。 |
| V0-4 | **Minor** | `ValidateBoardCells` / `ValidateBenchCells` / `ValidateShopCards` 在 `frame.BoardCells` 为 null 时会抛 `NullReferenceException`（`ValidateCounts` 只记录错误不阻止后续遍历）。需在循环前加 null 检查或提前 return。 |
| V0-5 | **Nit** | `stage` 文档要求格式 `"d-d"`（如 `"3-2"`），代码仅检查非空。若需严格格式校验，应加正则 `^\d+-\d+$`。 |

---

## D02 — dxgi（未提供代码）

**结论: SKIP** — 未附源码，无法审查。

---

## 汇总

| 任务 | 结论 | Critical | Major | Minor | Nit |
|------|------|----------|-------|-------|-----|
| R0   | PASS-with-notes | 0 | 1 | 2 | 1 |
| E4   | PASS | 0 | 0 | 0 | 2 |
| V0   | PASS-with-notes | 0 | 2 | 2 | 1 |
| D02  | SKIP | — | — | — | — |

**最需优先修复**: R0-1（数据丢失风险）、V0-1（字段名不一致阻塞下游）、V0-4（NRE 崩溃）。