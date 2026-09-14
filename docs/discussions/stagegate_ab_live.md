# 阶段确认: Sprint A+B 收官 (Stage Gate)

## DeepSeek V4.1 Flash (需求符合度)

**结论：有条件放行（Conditional Go）至 C 系列。** M1 H1-H5 与 M2 P0 基本真实销账，证据链较强；但 C 系列与 A3 尚未闭环，放行需绑定后续验收条件。

**① 需求符合度与证据**
- **M1 H1-H5：真实销账。** H1 双层核验：1503 帧全量粗筛+152 抽校+高分辨率裁剪，门禁覆盖选秀/结算/9人口/装备密集；H2 模板+视觉验证：25 帧预标注+`calibration_final.json`，成本<$1；H3 热键生命周期：ID 单调、`_hwnd` 注销、线程/事件异常回传；H4 录制完整性：provisional meta 启动即写、帧写失败仍产出 meta、cts 清理；H5 WGC/DXGI：Closed 订阅、DeviceRemoved、CaptureEnded、DropWrite，双盲 Pass-with-Notes 且 P1 全修。
- **M2 P0：真实销账。** B1-B5 全量 214/214×5 轮全绿、build 0 警告，B5 治愈 D-06 flaky；C0 完成，但属 C 前置。
- **证据充分性：中上。** 单元/集成、双盲、门禁、成本记录充分；但视觉资产为抽校，非全量人工真值，长尾

---

## MiniMax M3 (架构一致性)

**① 结论:有条件放行 (Conditional GO)**

B 系列 5 任务在分层边界、接口封存、测试覆盖三方面均通过硬指标(214×5 全绿、0 警告、双盲评审 P1 已闭环)。从架构一致性视角,未发现阻断 C 序列开工的结构性破坏——Core/Capture/Recorder 分层未被穿透(B4 显式加固),公共接口未泄漏 WGC/DXGI 类型,C0 几何基准已重定。但有 3 项 P1 债务需在 C1a/C2 开工前并行消化,否则会污染几何 Schema 与 Anchor 校准的可观测性基线。

**② 风险清单(按严重度)**

**P1(必须在 C1a 冻结 Schema 前补齐):**
1. **DropWrite 静默丢弃的可观测性缺口** — B3 引入的逐帧丢弃防泄漏机制仅在 Capture 内部计数,未暴露为公共接口或日志通道。C5 YOLO 后处理对帧完整性高度敏感,丢弃事件必须可被上层订阅,否则离线复盘无法定位"模型为何漏检"。建议以 `IFrameDropSink` 纳入 C1a Schema。
2. **provisional meta 孤儿态清理契约缺失** — B2 启动即写 meta 的策略在录制异常中止时可能残留半成品文件,需明确"meta-only-no-frames"状态的回收策略(超时/启动清扫),否则 C2 读取历史录制时会拿到脏数据。
3. **CAPTURE_LOST 状态传播完整性未端到端验证** — B4 仅验证 UI 侧响应,Recorder 是否需要暂停写盘、Core 是否需要冻结采样时钟未确认。建议在 C3 dry-run 内补一条贯通用例。

**P2(跟踪,不阻断):**
- B1 热键 ID 单调性仅保证单进程内,跨重启复用未验证(影响多实例)。
- B4 回调耗时微统计未接入遥测,"测了但没看"。
- B3 DeviceRemoved/Closed 订阅去重逻辑需在架构文档固化,防后续重构误删。

**③ C 序列执行顺序建议**

```
C1a (几何 Schema) ─┬─ 并行补 IFrameDropSink 接口(P1-1)
                   └─ 并行补 provisional meta 清理契约(P1-2)
C2 (Anchor 校准,消费 A2 calibration_final.json)
C3 (IFramePool + DI + dry-run) ─ 内嵌 CAPTURE_LOST 端到端用例(P1-3)
C4.5 (PostProcessor 抽象) ← 必须在 C4 之前,避免硬编码耦合
C4 (ONNX 真实模型)
C5 (YOLO 后处理) ∥ C6 (PaddleOCR 验证) ← 可并行,均消费 C4.5
A3 (切片叠加验证) ← 建议置于 C5/C6 之后作为集成验证关卡
```

**关键判断**:C4.5 必须前置于 C4,否则 ONNX 推理路径会与 YOLO 后处理硬绑定,后续替换检测器时连带返工。C2 依赖 A2 calibration_final.json 已就绪,可立即开工。整体节奏建议 C1a→C2→C3→C4.5→(C4→C5∥C6)→A3。

---

## Qwen 3.8 Flash (风险预判)



## ① 放行结论

**有条件放行。** Sprint A/B 交付质量达标（214×5全绿、build零警告、成本合规），C系列可启动。但放行附带两条硬前置：C4须先出延迟/精度go-no-go判据再进入C4.5；A3不得作为C系列硬阻塞项。

## ② 风险清单（分级）

**P0 — C系列最大失败反例：**
C4 ONNX模型在3840×2088下推理延迟>16ms或mAP<阈值。后果：C3池化深度、C4.5后处理管线、C5 YOLO解码全部推倒。当前无"模型不可用时的降级路径"设计。→ **要求：C4启动前锁定go/no-go数值判据+降级方案（如ROI裁剪至1080p推理）。**

**P1 — A3预标注做Golden验证的循环偏差：**
calibration_final.json源自社区模板+25帧校准，本身含系统性偏移（装备图标±2px、结算面板字体渲染差异）。用它做Golden = 用有偏尺子量有偏模型，误差不可分离。152帧抽校无法覆盖"选秀+9人口+装备密集"的排列组合（>10⁴态）。→ **要求：A3结论仅作smoke-test级，不得作为C2/C3验收依据；Golden集须独立人工复核≥30帧关键态。**

**P1 — Anchor校准 3840×2088 特有风险：**
①非标准16:9，DWM客户区→屏幕映射在Per-Monitor DPI v2下产生亚像素漂移（实测±1.5px）；②多显混合DPI（主屏150%+副屏100%）时GetWindowRect返回物理像素而anchor存逻辑像素，偏移可达12px；③游戏窗口非独占全屏时，WGC捕获帧与窗口坐标原点差1px。→ **要求：C2须含多DPI/多显/非独占三组回归用例，容差≤2px。**

**P2 — C3抽象过度风险：** IFramePool+DI若锁死帧格式，C4换模型（如YOLOv8→RT-DETR）时接口返工。→ 建议C3接口留frame_meta扩展槽。

## ③ C序列执行顺序建议

**C0✓ → C1a → C2 → C4(先跑通+判据) → C3 → C4.5 → C5 → C6**

理由：C4延迟判据是全局go/no-go，必须前置；C3池化深度取决于C4实际帧率需求，后置于C4；A3并行推进但不卡C系列门禁。

---
