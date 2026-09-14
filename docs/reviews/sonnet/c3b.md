# C3b Review - Claude Sonnet 5

# 独立 Full Review: C3b CLI Vision 装配

## 结论
整体装配基本可用，测试全绿，但存在**1个高危资源泄漏**、**2个中危设计缺陷**及若干低危问题，不建议直接判定为"完成"。

## 分级问题

**🔴 高危**
- **StubInferenceEngine 泄漏路径**：`--probe` 分支中 `GetRequiredService<IOnnxInferenceEngine>()` 拿到的引擎从未 `Dispose()`。虽然 Stub 的 Dispose 是空实现无影响，但若替换为真实 `OnnxInferenceEngine`（持有 native session），此路径会造成句柄/内存泄漏，且 `return` 提前退出未经过 `using host` 的常规 Dispose 流程干扰（host 的 Dispose 会连带释放 DI 容器内单例，此处**恰好**兜底，但属于隐式依赖，脆弱）。

**🟡 中危**
- **FrameDropped 计数无消费出口**：`_droppedThrottled`/`_droppedChannelFull` 仅在面板渲染时读取，`RunSimpleConsoleLoop`（非交互式/重定向输出场景）不会周期刷新面板，导致该场景下丢帧遥测完全不可见，违背"遥测接线"验收初衷。
- **CliVisionOptions 与 Vision 注册顺序耦合隐患**：`AddChanSightVision()` 先注册默认 `IOnnxInferenceEngine`，再按需用 `AddSingleton` 覆盖为 Stub。这依赖 DI 后注册覆盖前注册的行为（Microsoft.DI 中默认取最后一次注册），未来若 `AddChanSightVision` 内部改用 `TryAddSingleton` 或注册顺序调整，覆盖会静默失效且无编译期提示。

**⚪ 低危**
- `CreateStatusPanel` 中 `duration` 计算的 if/else 分支逻辑完全相同（`_isRecording` 与非 recording 分支计算式一致），属死代码，应合并或修正意图。
- `--probe` 路径中 `gate.Note` 硬编码依赖 `OnnxInferenceEngine.EvaluateLatencyGate` 静态方法，与 Stub 引擎耦合较松，但 Stub 场景下 `thresholdMs: 16.0` 语义（真实推理阈值）套用在零成本 Stub 上会永远 Passed，测试意义有限，建议标注或跳过。
- `StubInferenceEngineTests.RunInference_ReturnsSingleElementTensor` 中前半段 `act.Should().NotThrow()` 与后半段重复调用，冗余但无害。

## 建议
1. `--probe` 分支显式 `using var engine = ...` 或改造为 scope 内获取以保证真实引擎场景下资源确定性释放。
2. `RunSimpleConsoleLoop` 增加定期日志输出丢帧计数，或至少退出时打印汇总。
3. Vision 覆盖注册改用显式 `Replace`/`TryAdd` 模式，避免隐式顺序依赖。