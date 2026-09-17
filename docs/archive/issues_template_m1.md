# 【铲瞳 (ChanSight)】GitHub Issues 模板与批量创建指南

本文档汇集了 **Milestone 1 (实机录制与数据采样工具)** 所需创建的具体 Issue 内容，支持在 GitHub 网页端快速导入或通过脚本创建。

---

## Issue #1: [Core] 搭建 C# (.NET 8/9) 多工程解决方案与基础框架
**Labels**: `infrastructure`, `M1`
```markdown
### 目标
搭建铲瞳 (ChanSight) 的 .NET 8/9 多项目分层解决方案骨架，为屏幕抓取、对局录制与后续视觉推演提供坚实基础设施。

### 任务清单
- [ ] 创建 `ChanSight.sln`
- [ ] 划分工程结构：
  - `ChanSight.Core` (核心领域实体与接口)
  - `ChanSight.Capture` (屏幕/窗口高速抓取管道)
  - `ChanSight.Recorder` (视频编码与数据集采样)
  - `ChanSight.Cli` (命令行录制工具)
  - `ChanSight.Tests` (单元测试)
- [ ] 配置 `Directory.Build.props` (统一 C# 12/13, Nullable, 性能优化参数)
- [ ] 引入基础 NuGet 依赖：`OpenCvSharp4`, `OpenCvSharp4.runtime.win`, `System.Threading.Channels`

### 验收标准
- `dotnet build` 一键编译成功，工程引用清晰解耦。
```

---

## Issue #2: [Capture] 金铲铲/模拟器游戏窗口自动发现与 DPI 适配绑定
**Labels**: `capture`, `win32`, `M1`
```markdown
### 目标
实现能够自适应识别主流安卓模拟器（MuMu、雷电、夜神等）及游戏客户端窗口句柄的高精度定位器。

### 任务清单
- [ ] 基于 Win32 P/Invoke 实现 `WindowFinder`，支持模糊匹配“金铲铲之战”及各类模拟器标题
- [ ] 支持根据进程名（PID/ProcessName）和窗口句柄（HWND）精准绑定
- [ ] 处理 Windows Per-Monitor DPI 缩放，使用 `DwmGetWindowAttribute` 获取真实画面客户区尺寸（排除阴影边框）
- [ ] 监听窗口生命周期（移动、尺寸缩放、最小化、关闭回调）

### 验收标准
- 启动后 1 秒内自动寻找到游戏窗口并获取绝对坐标与真实物理分辨率。
```

---

## Issue #3: [Capture] Windows.Graphics.Capture (WGC) 硬件加速高速捕获引擎
**Labels**: `capture`, `gpu`, `M1`
```markdown
### 目标
基于 Windows 10/11 现代 WGC API 构建低延迟、GPU 直出的高速窗口捕获引擎。

### 任务清单
- [ ] 封装 WinRT `GraphicsCaptureSession` 与 `Direct3D11CaptureFramePool`
- [ ] 支持窗口内容直采（即使被其他窗口遮挡亦不影响捕获）
- [ ] 实现帧率节流控制器（可自由配置 10 / 30 / 60 FPS）
- [ ] 将捕获的 D3D11 Surface 转换为 BGR 格式的 `OpenCvSharp.Mat`
- [ ] 引入 DXGI Desktop Duplication 作为向下兼容降级备选

### 验收标准
- 1080P 分辨率下稳定 30~60 FPS 连续截取，端到端捕获延迟 < 5ms，CPU 占用 < 5%。
```

---

## Issue #4: [Recorder] 异步高吞吐对局录制管道 (Frame Buffer & Video Writer)
**Labels**: `recorder`, `pipeline`, `M1`
```markdown
### 目标
实现支持连续对局录制的高吞吐异步视频存储管道，输出标准化 MP4 对局录像与元数据，并支持 F6 全局启停。

### 任务清单
- [ ] 基于 `System.Threading.Channels` 搭建异步生产者-消费者无锁队列（`DropOldest` 丢帧策略防内存积压）
- [ ] 实现全局热键监听：`F6` 键作为程序录制【开始 / 停止】的主开关
- [ ] 实现 `VideoRecorderService`：支持一键开始、暂停、恢复、停止对局录制
- [ ] 支持输出标准 MP4 / AVI 视频格式（OpenCV VideoWriter 编码）
- [ ] 生成对局元数据文件 `meta.json`（录制开始时间、帧率、总帧数、分辨率、对局时长）

### 验收标准
- 按下 `F6` 可无缝开始/停止录制；30 分钟对局录制无花屏/无断裂，内存占用稳定 < 200MB。
```

---

## Issue #5: [Dataset] 对局数据集采样器 (按秒自动采样 + 自定义快照热键)
**Labels**: `dataset`, `tooling`, `M1`
```markdown
### 目标
在录制对局的同时，自动按时序或特定快捷键抽取高质量单帧图片，为后续 YOLO/OCR 模型训练构建原始数据集。

### 任务清单
- [ ] 实现定时快照采样器：按预设间隔（如每 2 秒 / 每 5 秒）提取单帧高清图片（PNG / JPG）
- [ ] 实现单帧快照专用快捷键（如 `F7` / `F8`，与 `F6` 启停完全解耦）
- [ ] 自动规范存储目录：`datasets/recordings/{session_id}/frames/{timestamp}_{index}.jpg`

### 验收标准
- 自动采样与手动单帧快照响应延迟 < 50ms，图片清晰无拉伸变形，可直接作为后续 YOLO/OCR 标注训练样本。
```

---

## Issue #6: [CLI] 命令行交互录制工具与实机测试运行
**Labels**: `cli`, `ux`, `M1`
```markdown
### 目标
交付用户即可运行的 CLI 交互式工具，具备实时状态监控与对局数据产出能力。

### 任务清单
- [ ] 在 `ChanSight.Cli` 中实现交互式控制台菜单（列出窗口、开始录制、手动快照、实时 FPS 监控）
- [ ] 接入 `Spectre.Console` 实现美观的实时帧率、磁盘写入速度与录制状态仪表盘
- [ ] 编写使用说明文档与实机录制测试指南

### 验收标准
- 运行 CLI 命令即可一键启动录制并看到实时监控统计，对局结束后产出可播放视频与帧数据集。
```
