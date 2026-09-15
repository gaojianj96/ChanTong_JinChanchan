# Retro Review E2/R1/V3 - Claude Sonnet 5

## 任务 E2(状态机) — Pass-with-Notes

- **P1(中)** `Update()` 忽略传入 `next.Version`,强制用 `_current.Version+1`,与 `UpdateOpponent`/`Transition` 语义一致,但方法签名暗示可自定义版本,容易误用;建议文档注明或改为校验。
- **P2(低)** `RecognitionToGameStateAdapter.Apply` 每帧都调用 `_manager.Update(...)`,不做 diff/脏检查,高频调用下版本号会飞速增长,但无功能问题,仅性能提示。
- **P3(低)** `IsTransitionAllowed` 用字典硬编码转移表,合理且测试覆盖较全(含非法自转移),状态机设计与测试一致,无阻塞问题。

结论:逻辑与测试自洽,并发安全(volatile+不可变record)达标,建议关注 Version 语义文档化。

---

## 任务 R1(回放加载) — Pass

- **P1(低)** `LoadAsync` 对 `JsonSerializer.Deserialize<MatchEvent>` 返回 `null`(如输入字面量 `null`)有处理,但未测试该分支(仅测试畸形JSON),覆盖略有缺口。
- **P2(低)** 空行判断用 `TrimEnd('\r').Trim()` 后 `Length==0` 跳过,但重复 trim 略冗余,非缺陷。
- **P3(低)** `ValidateTimeline` 与 `LoadAsync` 的 Seq 校验逻辑重复(去重检测在 Load,连续性检测在 Validate),两者职责边界清晰,测试也分离验证,设计合理。

结论:实现严谨,测试全面(排序/重复/坏行/缺文件/空文件/时间倒退等),无阻塞问题。

---

## 任务 V3(VLM适配器) — Pass-with-Notes

- **P1(中)** `OpenRouterVlmClient` 中 `MaxAttempts=2` 与 `VlmRecognitionAdapter.CompleteWithSingleRetryAsync` 的 `MaxAttempts=2` 叠加,实际最坏情况会重试 4 次网络请求,超时风险叠加(60s×最多4次),建议明确总重试预算或在文档中说明分层重试设计意图。
- **P2(中)** `ReadCompletionContentAsync` 在非 2xx 情况下不会被调用(已被上层短路),但解析阶段抛出的 `VlmUnavailableException` 未被 `CompleteAsync` 的 catch 块捕获(该 catch 只捕获 `HttpRequestException`/`TaskCanceledException`),会直接冒泡跳过重试循环,导致该类失败不会重试,与注释"retries once on transient failures"不完全一致。
- **P3(低)** `VlmUnavailableException` 缺少无参构造函数(常规异常约定),影响可空,非阻塞。

结论:核心解析/降级/模糊纠错逻辑测试完备,但异常分层与重试次数设计需澄清,建议下一轮补充集成测试验证真实重试路径。