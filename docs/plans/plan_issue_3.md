# Issue #3 Implementation Plan: [Capture] Windows.Graphics.Capture (WGC) 硬件加速高速捕获引擎

## 1. 任务背景与目标
- **目标 Issue**: `Issue #3: [Capture] Windows.Graphics.Capture (WGC) 硬件加速高速捕获引擎`
- **所属阶段**: `Milestone 1: 屏幕捕获与对局录制采样工具`
- **核心目的**: 基于 WinRT `Windows.Graphics.Capture` (WGC) 与 Direct3D11 实现对金铲铲/模拟器游戏窗口的 GPU 直出捕获，支持即使窗口被遮挡也能稳定以 30~60 FPS 连续抓取，并直通写入 `System.Threading.Channels` 有界管道，同时支持 DXGI 降级。

---

## 2. 详细技术实现方案

### 2.1 依赖与互操作层
- 引入 Direct3D11 / DXGI 相关互操作接口（`ID3D11Device`, `ID3D11Texture2D`, `IDXGISurface`），实现从 WinRT `Direct3D11CaptureFramePool` 提取 GPU 帧。
- 提供 `IGraphicsCaptureItemInterop` 互操作，根据 `IntPtr HWND` 创建 `GraphicsCaptureItem`。

### 2.2 核心服务组件 (`src/ChanSight.Capture/Services/`)
1. **`WgcCaptureService` (`IScreenCaptureService`, `IFrameSource`)**:
   - `StartCapture(WindowTarget target, CaptureOptions? options)`: 启动 WGC 捕获会话。
   - `FrameArrived` 事件驱动处理：从帧池中获取 D3D11 纹理，复制并映射为 BGRA/BGR 内存，封装为 `CapturedFrame`。
   - 帧率控制 (FPS Throttler)：按设定目标（10 / 30 / 60 FPS）精准丢弃多余帧，控制带宽。
   - 接入有界 Channel (`BoundedChannelCapacity = 2`, `DropOldest`)。
   - `StopCapture()`: 安全注销事件、释放 FramePool、Session 与 D3D11 资源。
2. **`DxgiCaptureService`**:
   - 封装 `IDXGIOutputDuplication` 作为 WGC 不可用时的向下兼容备选。

### 2.3 单元测试与 Mock 验证
- 在 `tests/ChanSight.Tests/Capture/CaptureServiceTests.cs` 中实现生命周期、背压丢帧与多线程启停测试。
