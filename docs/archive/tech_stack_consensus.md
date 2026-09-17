# 【铲瞳 (ChanSight)】C++ vs C# 架构选型深度研讨总结

## 1. 核心结论与选型共识
针对多平台开发难度与维护成本的考量，基础讨论组三大模型（DeepSeek V4 Flash、MiniMax M3、Qwen 3.7 Flash）一致认为：
> **对于桌面端 AI 视觉感知与 Overlay 辅助系统，C# (.NET 8/9 + Avalonia UI + ONNX Runtime) 在开发效率、跨平台维护成本、热重载调试以及现代 GUI 体验上远胜纯 C++ 全栈，且能保证极其优异的性能（损耗 < 5%）。**

---

## 2. 关键技术维度深度对比

| 评估维度 | 纯 C++20 全栈 | C# (.NET 8/9 + Avalonia UI) | 差异与结论 |
| :--- | :--- | :--- | :--- |
| **跨平台支持 (Win/macOS/Linux)** | 极难（需维护 Win32/DirectX、Cocoa/Metal、X11/Wayland 三套图形与 UI 栈） | **极优**（Avalonia UI 跨平台 XAML，一套 UI 代码覆盖多端） | C# 维护成本降低 **50%+** |
| **视觉推理 (ONNX Runtime / DirectML)** | 原生 C++ API | **微软官方一等公民** (`Microsoft.ML.OnnxRuntime`)，底层即 C++ | 推理性能损耗 **< 5%**，几乎无差别 |
| **屏幕捕获与 Overlay 性能** | DXGI 延迟 < 2ms | P/Invoke 或 WGC API 配合 `Span<T>`/`ArrayPool`，延迟 ~2-4ms | 均可轻松达成 60 FPS |
| **开发与调试效率** | 编译慢（5-15分钟），调试困难，无热重载 | **秒级增量编译，支持 Hot Reload，NuGet 生态开箱即用** | C# 开发迭代速度快 **2~3 倍** |
| **内存与发布体积** | 手动内存管理，产物 < 20MB | 支持 **Native AOT** 编译，无需安装运行时，零 GC 抖动，单文件分发 | 现代 .NET AOT 体验接近原生 |

---

## 3. 【铲瞳 (ChanSight)】最终推荐架构方案

```
ChanSight (铲瞳)
├── UI 表现与交互层: Avalonia UI 11+ (C# XAML, 跨平台透明置顶 Overlay)
├── 业务与决策引擎: C# (.NET 8/9, 异步流水线 + 牌库历史 + 概率推算)
├── 视觉推理引擎: Microsoft.ML.OnnxRuntime (DirectML EP / CoreML EP / CPU)
├── 模型层: YOLOv11n ONNX + PaddleOCR-v4-Mobile ONNX
├── 底层系统交互: 
│   ├── Windows: Windows.Graphics.Capture API / DXGI (P/Invoke)
│   └── macOS: ScreenCaptureKit
└── 发布与部署: dotnet publish -c Release -r win-x64 --self-contained / Native AOT
```
