# 技术栈选型研讨文档 (Tech Stack Discussion)

## 1. 讨论背景
- 用户明确指示：**不倾向于使用 Python** 作为项目的核心开发语言。
- 项目核心需求：高帧率屏幕截取（Windows DDA/DXGI/Win32）、低延迟图像处理与目标检测、内存安全、实时透明 UI 遮罩覆盖（Overlay）、多模型/API 编排。

---

## 2. 基础讨论组针对「非 Python 方案」的各视角评估

### 视角 A：DeepSeek V4 Flash（底层性能与系统架构视角）
- **推荐方案：Rust / C++**
- **核心理由**：
  - 屏幕捕获与视觉识别属于高吞吐、低延迟场景（DirectX Desktop Duplication API / DXGI / Windows Graphics Capture）。
  - Rust 具备极高的运行效率与零成本抽象（Zero-cost Abstractions），通过 `windows-rs` / `wgpu` / `ort` (ONNX Runtime Rust bindings) 可无缝调用 GPU 加速视觉模型。
  - 内存安全保证程序在长时间挂机监控下不发生内存泄漏。

### 视角 B：MiniMax M3（工程落地与跨端/UI 表现视角）
- **推荐方案：Rust + Tauri / C++ + Qt / Go + Webview**
- **核心理由**：
  - 游戏屏幕监控需要极轻量、置顶、透明的悬浮窗（Transparent Overlay）。
  - Tauri (Rust + 前端 HTML/Canvas) 体积极小（< 15MB），CPU/内存占用极低，非常适合作为游戏悬浮助手。
  - Go 语言在 API 编排与多模型通信上极为高效，但在底层屏幕抓取与图形渲染上生态不如 Rust/C++。

### 视角 C：Qwen 3.7 Flash（生态适配与维护成本视角）
- **推荐方案：Rust 生态体系 (主力推荐) 或 C# (.NET 8/9 / WPF)**
- **核心理由**：
  - 若追求 Windows 平台原生最快集成：C# (.NET 8) 拥有非常完善的 Windows 原生桌面生态与 DirectML 支持。
  - 若追求高性能、跨平台极客风格与现代化：Rust 是当前最符合视觉实时流 + 嵌入式轻量 UI + 高性能 ONNX 推理的最佳选择。

---

## 3. 待决策的技术栈备选方案

| 方案编号 | 技术栈组合 | 图像捕获/推理层 | UI/悬浮窗层 | 优势与特点 |
| :---: | :--- | :--- | :--- | :--- |
| **方案 1 (推荐)** | **Rust 全栈** | `windows-capture` + `ort` (ONNX Runtime) | `Tauri` / `egui` | 极致性能、极低内存占用（<50MB）、原生 DXGI 截屏、零 GC 停顿 |
| **方案 2** | **C# / .NET 8** | `Windows.Graphics.Capture` + `Microsoft.ML.OnnxRuntime` | `WPF` / `WinUI 3` | Windows 原生集成度最高，开发与调试极为方便 |
| **方案 3** | **C++ / CMake** | `DirectX / D3D11` + `OpenCV C++` + `TensorRT/ONNX` | `ImGui / Qt` | 经典游戏辅助与工业级视觉架构，但代码维护成本相对偏高 |
