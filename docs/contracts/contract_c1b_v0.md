# Contract C1b v0: M3 服务契约 (Service Contracts)

> Tech Lead: DeepSeek V4 Pro 0813 #2 ｜ 状态: 设计定稿待 Planner 仲裁 ｜ 日期: 2026-09-12
> 上游: docs/plans/plan_cleanup_m3_final.md (C1b) ｜ 依赖: 无 (可并行开工)

# Contract C1b v0：服务契约

状态：设计冻结待实现  
依赖：C1a 几何元数据 Schema（仅坐标语义引用）  
硬约束：契约不得引入 ChanSight.Vision 之外的跨层依赖；Core 保持零重依赖。

## 1. 目标与范围

C1b 冻结不依赖真实 ONNX/OCR/YOLO 模型实现的基础服务契约，为 C2 Anchor 校准、C3 代码缺口补齐提供稳定边界。

- **本阶段冻结**：`IFramePool`、`IOnnxInferenceEngine`、`IPostProcessor<TDetection>`、`IAnchorCalibrator`。
- **仅 stub，不冻结输出细节**：`StubInferenceEngine`（C3 实现并注册）；`IYoloDetectorService`、`IPaddleOcrService` 保持空壳或暂不注册，等待 C4/C5 完成。
- **留到 C4 之后冻结**：`IYoloDetectorService`、`IPaddleOcrService`、`IYoloDatasetExporter` 的具体检测/识别输出结构，因依赖 C4.5 PostProcessor 抽象与 C5/C6 后处理结果。
- 已有稳定接口 `IDatasetCleanerService`、`IGridSlicerService`、`IPerceptualHashService`、`IRoiMapperService` 本次仅确认签名，不修改。

Core 中的 `IFramePool` 不依赖 OpenCvSharp/ONNX/WGC，仅使用 `System.Memory` 与 `System.Buffers`。

## 2. 接口契约

| 接口 | 方法签名 | 输入输出契约 | 不变量 | 归属项目 |
| --- | --- | --- | --- | --- |
| `IFramePool` | `bool TryRent(out IFrameLease lease); void Return(IFrameLease lease); int Capacity { get; } int RentedCount { get; }` | `IFrameLease : IDisposable` 暴露 `Memory<byte> Data`、`int SlotId`。容量不足返回 `false`；`Dispose` 等价于 `Return`。 | `RentedCount <= Capacity`；归还后租约不可再访问；稳态租还零分配；实现线程安全。 | Core |
| `IOnnxInferenceEngine` | `IReadOnlyDictionary<string, OrtValueTensor> Run(IReadOnlyDictionary<string, OrtValueTensor> inputs, CancellationToken ct = default); InferenceDevice Device { get; }` | `OrtValueTensor` 包含 `Name`、`Shape`、`ElementType`、`Device`。CPU 张量可读内存；DirectML 张量不跨设备隐式拷贝。 | `Run` 串行，实例线程安全；张量必须释放；设备枚举仅 `Cpu` 或 `DirectML`；释放后调用抛 `ObjectDisposedException`。 | Vision |
| `IPostProcessor<TDetection> where TDetection : IDetection` | `IReadOnlyList<TDetection> Process(IReadOnlyList<TDetection> candidates, PostProcessOptions options, CancellationToken ct = default);` | `TDetection` 包含原图坐标 `Box`、`Confidence`、`ClassId`；`options` 含 `NmsThreshold`、`ConfidenceThreshold`、`RoiBounds`。 | 输出数量 <= 输入数量；只对同类做 NMS；输出坐标不超出 ROI 边界；空输入返回空列表。 | Vision |
| `IAnchorCalibrator` | `AnchorCalibrationResult Calibrate(Mat frame, CalibrationOptions options, CancellationToken ct = default);` | `AnchorCalibrationResult` 含 `bool IsValid`、`Mat? Transform`、`float Confidence`、`IReadOnlyList<Point2f> AnchorPoints`。输入 `Mat` 只读。 | 有效时 `Confidence ∈ [0,1]` 且 `Transform` 非 null；无效时 `Transform == null`、`Confidence == 0`；输出 `Mat` 由调用方释放。 | Vision |

`Mat` 为 OpenCvSharp 类型，仅在 Vision 项目内使用。坐标约定以 C1a 几何元数据为准：除非 option 显式说明，所有 Box 与 Anchor 坐标均为原图坐标系。

## 3. 依赖注入与注册图

`AddChanSightVision()` 仅注册 Vision 项目内服务：

| 服务 | 实现 | 生命周期 | 理由 |
| --- | --- | --- | --- |
| `IOnnxInferenceEngine` | `StubInferenceEngine` | Singleton | 模型/设备上下文仅需一份；内部串行锁保护并发调用。 |
| `IAnchorCalibrator` | `ClassicAnchorCalibrator`（C2） | Singleton | 无状态角点检测；参数只读。 |
| `IPostProcessor<TDetection>` | 具体后处理器（C4.5/C5） | Singleton | 无状态；阈值由 `PostProcessOptions` 每次传入。 |
| `IDatasetCleanerService`、`IGridSlicerService`、`IPerceptualHashService`、`IRoiMapperService` | 已有实现 | Singleton | 无状态工具；实现需保证线程安全。 |
| `IYoloDetectorService`、`IPaddleOcrService` | 暂不注册 | — | 输出结构未冻结，待 C4/C5 后加入。 |

`IFramePool` 不由 `AddChanSightVision()` 注册，避免跨层依赖；它由 Core 提供的 `AddChanSightCore()` 注册为 Singleton，容量配置由启动期决定。

**Stub 替换时机**：C3 使用 `StubInferenceEngine` 完成 CLI `--vision-dry-run`。C4 实现真实 `OnnxInferenceEngine`（DirectML + CPU 回退）后，仅替换 DI 实现，接口不变，调用方不受影响。

## 4. Acceptance Criteria

C1b 完成的验收以契约审查与后续 C3 测试类型为准：

1. **依赖审查**：Core 项目不出现 OpenCvSharp/ONNX/WGC 引用；Vision 契约不引用 Capture/Recorder/Cli。
2. **IFramePool 契约测试**：容量 0/1/N 下租还循环 10,000 次；`GC.GetAllocatedBytesForCurrentThread` 增量为 0；双还、超容量按契约返回或抛出明确异常。
3. **StubInferenceEngine 测试**：给定固定输入形状输出可预测张量；`Device == Cpu`；`Dispose` 后 `Run` 抛 `ObjectDisposedException`。
4. **IPostProcessor NMS 测试**：两个高 IoU 同类框保留一个；不同类不抑制；ROI 外候选被过滤；空输入返回空列表。
5. **IAnchorCalibrator 测试**：合成角点帧得到有效 `Transform`；`Confidence` 在 `[0,1]`；无锚点帧返回 `IsValid == false`。
6. **DI 测试**：解析 `IOnnxInferenceEngine` 两次为同一实例；`IFramePool` 由 Core 注册可解析；`AddChanSightVision()` 不尝试解析 Core 专用实现。
7. **CLI 冒烟**：C3 完成后执行 `--vision-dry-run` 可跑通单帧管线，不触发真实模型。

## 5. 风险与不变量清单

- **并发**：`IFramePool` 与 `IOnnxInferenceEngine` 必须线程安全。推理引擎可作为单生产者/单消费者使用，但实现不得依赖调用方锁。
- **Mat 生命周期**：输入 `Mat` 在方法返回后可由调用方释放；输出 `Mat` 由调用方负责释放；禁止跨线程不克隆共享 `Mat`。
- **租约管理**：`IFramePool.Return` 必须防御双还和无效租约，避免池状态破坏；池泄漏测试需覆盖异常路径。
- **异常恢复**：任何推理、后处理或校准异常不得泄漏张量/租约；调用方采用 `try/finally` 释放资源。
- **零分配**：稳态帧处理路径避免 LINQ 装箱与临时大对象分配；NMS 列表可池化，但契约不强制实现细节。
- **硬约束**：所有契约变更若涉及 Core，必须保持 Core 零重依赖；Vision 契约不得暴露底层 ORT/DirectML 句柄。