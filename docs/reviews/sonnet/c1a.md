# C1a Review - Claude Sonnet 5

## 结论：**Pass-with-Notes**

---

### P1（应尽快修复)

1. **`Reader.Count >= Capacity` 前置判定存在竞态,与多写者场景不符**
   `WgcCaptureService` 的 channel 配置为 `SingleWriter = false`(多写者),但 `OnFrameReady` 中先读 `Count` 再 `TryWrite` 两步非原子。并发场景下两个线程可同时通过 `Count < Capacity` 检查,导致本应二者之一被判 `ChannelFull` 丢弃的帧被误判为写入成功(或反之丢弃了不该丢的帧),使得丢帧事件与实际队列状态不一致。既然已知 `DropWrite` 下 `TryWrite` 对丢弃亦返回 `true`,更稳妥的方式是让 `TryWrite` 返回值与"实际长度变化"配合判断,或直接改为 `SingleWriter=true`(WGC 回调本身是否单线程需确认)。当前测试只覆盖单线程顺序 `Publish`,未暴露该竞态。

2. **DXGI 路径的 `ChannelFull` 判定同样依赖 `Count>=Capacity` 前置**,但该 channel 是 `SingleWriter = true`,该场景下无竞态问题,不影响正确性——**两处实现应注明差异**,避免后续误将 Wgc 的模式复制到别处。

### P2（建议改进)

3. **`BoardGeometry.ComputeStaggerX` 用 `Math.Abs` 掩盖了 X 方向单调性假设**。若社区模板出现错位方向反转（如某行整体左移取代右移),`StaggerX` 仍会落入 `[45,70]` 区间通过测试,但几何含义已失真,建议保留符号或额外断言方向一致性。

4. **`BoardGeometry.CreateCanonical()` 中 28 个坐标点为硬编码 switch 表达式**，缺少来源可追溯性（校准报告/截图版本号只在 `Source` 字符串里,人工可改动且不会被校验一致性）。建议增加对 `Source` 版本号与坐标表的哈希绑定测试，防止未来改动坐标而忘记更新 `Source`。

5. **`FromJson` 只校验数量(28/9/5),未校验 `Row`/`Col`/`Index` 唯一性和范围**。畸形 JSON（如 28 个点全部 `Row=0,Col=0`）能通过校验，属于潜在数据完整性缺口。

6. **`WgcCaptureService.OnFrameReady` 中 `Interlocked.CompareExchange` 自旋更新 `_maxCallbackTicks` 缺少更新失败上限保护**——极端高并发下可能长时间自旋，建议加超时/放弃策略，当前非阻断性问题。

### P3（文档/可读性)

7. `DxgiCaptureService.CaptureLoopAsync` 中 `AccessLost`/普通异常都走同一个 `failed=true; break`，导致 `FrameDropReason` 枚举里的 `CaptureEnded` 只在 provider 侧触发,DXGI 侧的失败退出未记录任何 `FrameDropped`(合理，因为不是丢帧，是捕获终止)，但建议在 XML 注释里说明这个语义边界，减少后续维护者困惑。

8. 223×3 全绿的"×3"重复运行如果只是为了发现 flaky test（如上述竞态），建议改为压力/并发场景专项测试，而不仅是重复跑同样的顺序场景。

---

**总评**：几何 Schema 设计清晰、有效性校验到位；帧丢弃可观测机制思路正确、事件语义清楚，但 WGC 路径的多写者 + 非原子容量判定存在理论竞态，建议在合并前补充并发压测或改为原子操作后转为 Pass。