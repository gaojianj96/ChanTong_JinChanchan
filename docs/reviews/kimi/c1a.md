# C1a Review - Moonshot Kimi K3

**结论：通过（Approve）**。代码质量高，线程安全设计严谨，资源管理完善，测试覆盖核心场景。仅存在少量可维护性改进建议，无阻塞性缺陷。

---

### 🔴 严重（Blocker）
无。

### 🟡 建议（Major）
1. **魔数分散**（`BoardGeometry.cs`）：28个棋盘格坐标、9个备战席、5个商店位均为硬编码数值，建议提取为 `private static readonly` 数组或常量类，便于后续校准调整。
2. **重复逻辑**（`WgcCaptureService.cs` vs `DxgiCaptureService.cs`）：`ShouldAccept` 与帧丢弃逻辑（`FrameDropped`触发）完全重复，建议提取为 `FrameThrottler` 工具类或基类，避免双份维护。
3. **性能隐患**（`DxgiCaptureService.cs`）：`CopyTextureToFrame` 中每帧创建 `Staging Texture` 和 `Mat`，高频捕获时GC压力大。建议引入对象池（`ArrayPool<Mat>` 或复用 `Staging Texture`）。

### 🟢 提示（Minor）
1. **事件线程安全**：`FrameDropped?.Invoke` 在 `finally` 块外直接调用，若订阅者抛出异常会中断帧处理循环，建议添加 `try-catch` 保护或改用 `async void` 日志封装。
2. **资源释放兜底**：`DxgiCaptureService` 中 `duplication?.ReleaseFrame()` 在 `finally` 中调用，若 `AcquireNextFrame` 成功但后续代码抛异常，可能导致帧未释放（尽管 `acquired` 标志已缓解，建议确认 `ReleaseFrame` 的幂等性）。
3. **测试覆盖**：`BoardGeometryTests` 未覆盖 `FromJson` 的异常路径（如非法JSON格式、字段缺失），建议补充 `[Theory]` 测试用例。

### ✅ 亮点
- **线程安全**：`syncRoot` 保护状态变更，`Interlocked` 处理性能计数器，无锁竞争风险。
- **资源管理**：`DropWrite` 模式配合显式 `Dispose` 避免内存泄漏，`await` 链式释放确保异步清理。
- **可测试性**：`BoardGeometry` 提供 `CreateCanonical` 工厂方法，测试数据与生产代码解耦。