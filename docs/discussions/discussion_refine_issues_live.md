# ChanSight - Refined Milestone & Issues Discussion (Screen Capture & Recorder Priority)

## DeepSeek V4 Flash ($(@{name=DeepSeek V4 Flash; id=deepseek/deepseek-v4-flash; outFile=docs/raw_responses/out_refine_ds.json}.id))

根据您的调整意见与重构诉求，我们重新梳理了【铲瞳 (ChanSight)】项目的里程碑规划与 GitHub Issues 清单。核心原则是 **“优先交付可运行的实机录制与数据采样工具”**，确保在第 1 个里程碑就能获得真实对局数据，为后续视觉分析提供底座。以下为细化后的方案。

---

## 一、里程碑规划（共 4 个里程碑）

| 里程碑 | 名称 | 核心交付物 | 预期周期 |
|--------|------|------------|----------|
| **M1** | 屏幕捕获与数据采样工具 | 高精度窗口绑定、实时帧捕获、对局录制、帧序列/数据集输出 | 2~3 周 |
| **M2** | 数据预处理与标注流水线 | 帧裁剪、去重、图像增强、半自动标注（棋子/装备/羁绊） | 2 周 |
| **M3** | 视觉模型训练与离线推理 | 基于 OpenCvSharp + ONNX 的棋子检测、状态识别模型 | 3~4 周 |
| **M4** | 对局分析引擎与可视化 | 实时/离线分析面板、对局回放、数据统计、报告导出 | 2 周 |

> **说明**：M1 是用户最关心的“快速反馈”阶段，完成后即可开始采集真实数据；后续阶段可并行或迭代开发，但 M1 必须优先交付。

---

## 二、Milestone 1 详细 GitHub Issues 清单

以下为 M1 的细粒度 Issue 列表，每个 Issue 均包含标题、描述、验收标准（AC）。建议按顺序实现，部分可并行。

### Issue 1.1：项目骨架与基础模块搭建
- **描述**：创建 .NET 8/9 控制台或 WPF 项目，引入必要 NuGet 包（SharpDX、Vortice.Windows、OpenCvSharp4、System.Drawing.Common 等），建立项目结构（Core、Capture、Storage、UI 等命名空间），配置日志（Serilog/NLog）与异常处理。
- **验收标准**：
  - 项目可编译运行，无依赖冲突。
  - 日志输出到文件和控制台。
  - 基础配置（如默认帧率、输出路径）可通过 JSON 文件加载。

### Issue 1.2：DXGI/WGC 屏幕捕获引擎（核心）
- **描述**：实现基于 DXGI（IDXGIOutputDuplication）或 Windows.Graphics.Capture（WGC）的屏幕帧捕获器。支持：
  - 全屏捕获（用于调试）。
  - 指定窗口捕获（通过窗口句柄或进程名，如“金铲铲之战”）。
  - 帧率控制（可配置，如 30fps / 60fps）。
  - 帧数据以 `Mat`（OpenCvSharp）或 `Bitmap` 形式输出。
- **验收标准**：
  - 能稳定捕获指定窗口（即使窗口被遮挡或最小化？需明确：建议捕获窗口内容，即使最小化也可捕获，但需处理窗口不存在的情况）。
  - 捕获帧率误差 ≤ ±2fps。
  - 连续运行 30 分钟无内存泄漏，CPU 占用 ≤ 15%（i7-12700 参考）。

### Issue 1.3：窗口绑定与自动定位
- **描述**：实现窗口查找与绑定逻辑：
  - 通过进程名（`League of Legends`? 实际金铲铲进程名需确认）或窗口标题（含“金铲铲”关键字）自动找到游戏窗口。
  - 支持手动输入窗口句柄或进程 PID。
  - 窗口尺寸变化时自动更新捕获区域（可选：固定分辨率或跟随窗口）。
  - 提供绑定状态回调（成功/失败/窗口关闭）。
- **验收标准**：
  - 启动后 2 秒内找到并绑定窗口。
  - 窗口最小化时仍可捕获（DXGI 支持）。
  - 窗口关闭时触发通知，停止捕获。

### Issue 1.4：对局录制控制器（开始/暂停/停止）
- **描述**：封装录制逻辑：
  - 启动录制：开始帧捕获并写入存储。
  - 暂停/恢复：保留捕获引擎但停止写入（节省磁盘）。
  - 停止录制：关闭捕获引擎，保存索引文件。
  - 支持录制时长限制（如 30 分钟自动停止）或手动停止。
- **验收标准**：
  - 录制过程中可随时暂停/恢复，帧序列连续无断裂。
  - 停止后生成完整的帧列表元数据（时间戳、帧序号、分辨率）。
  - 支持热键（如 F9 开始/停止）或命令行触发。

### Issue 1.5：帧序列存储与数据集输出
- **描述**：将捕获的帧保存为：
  - **图像序列**：PNG 或 JPEG（可配置质量），按 `{timestamp}_{frameIndex}.png` 命名，存放于日期/对局子目录。
  - **视频文件**：可选输出为 MP4（使用 FFmpeg 或 OpenCV VideoWriter），但优先保证帧序列便于后续标注。
  - 支持输出格式配置（如仅保存关键帧？可延后）。
  - 写入时使用异步队列，避免阻塞捕获线程。
- **验收标准**：
  - 30fps 录制 10 分钟，输出约 18000 帧 PNG，总大小可接受（需预估，建议默认 JPEG 90% 质量）。
  - 磁盘写入速度 ≥ 50MB/s，不丢帧。
  - 帧序列元数据文件（JSON/CSV）包含每帧的时间戳、原始尺寸。

### Issue 1.6：数据采样工具（手动/自动截取）
- **描述**：提供两种采样模式：
  - **手动采样**：用户按下热键（如 F6）时保存当前帧到“采样”目录，用于快速收集特定场景（如开局、选秀、战斗）。
  - **自动采样**：基于规则（如每 5 秒采样一帧，或检测到画面变化时采样），可配置。
- **验收标准**：
  - 手动采样响应延迟 ≤ 100ms。
  - 自动采样支持间隔设置（1~60 秒）。
  - 采样帧独立于录制帧，互不干扰。

### Issue 1.7：性能监控与调试面板（可选 UI）
- **描述**：若采用 WPF 或 WinForms，提供简单 UI：
  - 显示当前捕获帧率、已录制时长、磁盘使用量。
  - 显示绑定窗口缩略图（实时预览）。
  - 日志输出窗口。
  - 若为控制台版本，则仅输出统计信息到控制台。
- **验收标准**：
  - UI 不显著影响捕获性能（帧率下降 ≤ 5%）。
  - 缩略图更新频率可配置（如 1fps）。

### Issue 1.8：集成测试与稳定性验证
- **描述**：编写自动化测试脚本（或手动测试用例）：
  - 测试不同分辨率窗口（1920x1080, 2560x1440）。
  - 测试窗口最小化、移动、关闭场景。
  - 测试长时间录制（1 小时以上）的内存/CPU 稳定性。
  - 测试多实例（同时录制多个窗口？可暂不支持）。
- **验收标准**：
  - 所有测试用例通过。
  - 内存峰值 ≤ 500MB（录制 1 小时）。
  - 无异常崩溃。

---

## 三、Milestone 1 整体验收标准

1. **功能完整**：用户可通过命令行或简单 UI 启动/停止录制，绑定金铲铲游戏窗口，输出帧序列或视频文件。
2. **性能达标**：在主流配置（i5-12400 + 16GB RAM + 集显/独显）下，30fps 捕获不卡顿，CPU 占用 < 20%，内存 < 500MB。
3. **数据可用**：输出的帧序列可直接用于后续视觉模型训练（如标准标注工具可读取）。
4. **用户反馈**：用户能在 1 小时内完成首次录制并拿到真实对局数据。

---

## 四、后续里程碑简要规划

### Milestone 2：数据预处理与标注流水线
- **核心 Issue**：
  - 帧裁剪（去除 UI 边框，保留棋盘/商店区域）。
  - 图像去重（基于感知哈希）。
  - 半自动标注工具（基于 OpenCvSharp 的模板匹配或传统 CV 方法初步标注棋子位置）。
  - 导出为 COCO 标准标注格式。
- **验收**：能对 1000 帧数据完成自动预处理，人工修正量 < 20%。

### Milestone 3：视觉模型训练与离线推理
- **核心 Issue**：
  - 基于 OpenCvSharp 的 DNN 模块加载 ONNX 模型（自定义）。
  - 棋子检测、装备识别、羁绊状态识别。
  - 离线推理引擎（对帧序列批量分析）。
- **验收**：棋子检测 mAP ≥ 0.85，单帧推理时间 < 50ms。

### Milestone 4：对局分析引擎与可视化
- **核心 Issue**：
  - 对局事件提取（回合开始/结束、选秀、野怪）。
  - 阵容、经济、血量变化曲线。
  - 可视化面板（WPF 或 Web）。
  - 导出分析报告（PDF/HTML）。
- **验收**：能完整分析一场 30 分钟的对局，输出可读报告。

---

## 五、建议

1. **技术选型**：
   - 屏幕捕获优先使用 **DXGI**（`IDXGIOutputDuplication`），兼容性好、性能高；若需 UWP 兼容则备选 WGC。
   - 帧处理使用 **OpenCvSharp4** 的 `Mat` 对象，避免频繁 Bitmap 转换。
   - 异步写入使用 **Channel<T>** 或 **BlockingCollection**，确保捕获与存储解耦。

2. **早期验证**：
   - 在 Issue 1.2 完成后即可手动测试捕获帧率，不必等全部实现。
   - 建议先实现控制台版本，UI 可延后，以最快速度交付可运行工具。

3. **数据管理**：
   - 帧序列命名包含时间戳和游戏阶段（可通过图像识别初步判断，M2 再做），便于后续筛选。
   - 提供脚本（Python）辅助将帧序列转换为视频，方便用户预览。

4. **错误处理**：
   - 捕获引擎需处理 `Access is denied`（权限问题）、`DXGI_ERROR_ACCESS_LOST`（窗口切换）等常见异常，自动重试或提示用户。

5. **文档与示例**：
   - 每个 Issue 完成后更新 README，提供命令行参数示例和常见问题 FAQ。

---

以上是细化后的里程碑与 Issue 清单。M1 完成后用户即可投入实机录制，后续阶段可基于真实数据迭代。请根据实际开发资源调整优先级，建议从 Issue 1.1 → 1.2 → 1.3 → 1.5 → 1.4 → 1.6 的顺序推进。如有任何疑问，欢迎进一步讨论。

---

## MiniMax M3 ($(@{name=MiniMax M3; id=minimax/minimax-m3; outFile=docs/raw_responses/out_refine_mm.json}.id))

# 铲瞳 (ChanSight) — 里程碑重构与 M1 细化方案

## 一、调整原则与总体思路

**核心转变**：从"先建模后采数据"反转为"**先采数据后建模**"。M1 阶段交付一个**可立即上手的实机录制工具**，让用户在第 1 周就能产出真实对局数据，反哺后续所有视觉/决策模块的训练与验证。

**M1 阶段的设计哲学**：
- **可运行优先**：每一项 Issue 都必须能产出可演示的中间产物
- **接口先行**：核心模块以接口隔离，便于后续替换/升级
- **数据契约清晰**：录制产物的目录结构、命名、元数据从 Day 1 就固定
- **金铲铲特化**：窗口识别、采样策略需针对 TFT 对局节奏优化

---

## 二、里程碑重规划（6 个阶段）

| 阶段 | 名称 | 周期 | 核心交付 | 验证方式 |
|------|------|------|----------|----------|
| **M0** | 工程脚手架 | 0.5 周 | 解决方案结构、CI、日志、配置 | `dotnet build` + 单元测试通过 |
| **M1** | **屏幕捕获与对局录制工具** | **2–3 周** | **可运行的录制 CLI/GUI，产出真实数据集** | **录制 1 局完整对局，帧序列完整可回放** |
| M2 | 视觉预处理与棋盘定位 | 2 周 | 棋盘 ROI 提取、透视变换、模板库 | 在 M1 数据上自动裁剪出棋盘区域 |
| M3 | 棋子/装备/状态识别 | 3–4 周 | 模板匹配检测器，OCR 数字 | 在 M1 数据上 mAP > 0.85 |
| M4 | 对局状态机与策略引擎 | 3 周 | 阶段识别、经济计算、推荐算法 | 离线回放给出可解释建议 |
| M5 | 端到端集成与实时辅助 | 2 周 | 实时叠加层、低延迟流水线 | 实机延迟 < 200ms |

> **关键变化**：M1 不再是"基础设施"，而是**第一个有用户价值的产品**。

---

## 三、M1 详细 Issue 清单

### M1.0 — 工程脚手架（前置，1–2 天）

| Issue | 标题 | 验收标准 |
|-------|------|----------|
| **#1** | 解决方案结构与项目分层 | 创建 `ChanSight.sln`，包含 `Core` / `Capture` / `Recording` / `Dataset` / `Cli` / `Gui` / `Tests` 项目；`.editorconfig` + `Directory.Build.props` 统一风格 |
| **#2** | 日志与配置框架 | 集成 Serilog，支持文件 + 控制台输出；`appsettings.json` 加载录制参数（路径、FPS、编码器） |
| **#3** | CI 流水线 | GitHub Actions：`windows-latest` 上 `dotnet build/test` 通过；缓存 NuGet |
| **#4** | 文档骨架 | `README.md` 含项目说明、构建命令、录制工具快速上手；`docs/` 目录预留 |

---

### M1.1 — 窗口发现与绑定（核心，3–4 天）

| Issue | 标题 | 验收标准 |
|-------|------|----------|
| **#5** | 窗口枚举工具 `WindowEnumerator` | 通过 P/Invoke `EnumWindows` + `GetWindowText` + `GetWindowThreadProcessId`，列出所有可见顶层窗口的 `Title / PID / Bounds / ClassName`；提供单元测试覆盖 |
| **#6** | 金铲铲窗口识别器 `TftWindowMatcher` | 支持按进程名（`TencentGame*` / `LeagueClientUx*` / 自定义白名单）+ 窗口标题正则匹配；返回首个匹配窗口；可配置多规则 |
| **#7** | 窗口绑定抽象 `IWindowTarget` | 接口包含 `Handle / Bounds / ClientBounds / Dpi / IsForeground`；实现 `Win32WindowTarget`，处理 DPI 缩放（PerMonitorV2），物理像素与逻辑像素分离 |
| **#8** | 窗口状态监听 | `WindowStateMonitor` 监听最小化/关闭/失去焦点事件，触发回调；录制中窗口被遮挡时自动暂停 |
| **#9** | 窗口绑定单元测试 | Mock 测试 + 实机测试（手动启动金铲铲，验证能正确识别并绑定） |

**技术要点**：
- 使用 `Vanara.PInvoke` 或原生 P/Invoke 封装 Win32 API
- DPI 必须用 `SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)`
- `ClientBounds` 要用 `DwmGetWindowAttribute` 获取真实客户区（排除阴影）

---

### M1.2 — 捕获后端（WGC + DXGI 双引擎，5–6 天）

| Issue | 标题 | 验收标准 |
|-------|------|----------|
| **#10** | 捕获后端抽象 `ICaptureBackend` | 接口：`Start() / Stop() / FrameAvailable event / BackendName / IsSupported`；便于切换实现 |
| **#11** | WGC 捕获实现 `WgcCaptureBackend` | 基于 `Windows.Graphics.Capture`（WinRT），通过 `GraphicsCaptureSession` + `Direct3D11CaptureFramePool`；支持窗口/显示器捕获；NuGet 依赖 `Microsoft.Windows.SDK.Contracts` + `WinRT.Runtime` |
| **#12** | DXGI Desktop Duplication 备选 `DxgiCaptureBackend` | 基于 `Vortice.DXGI` 或 `SharpDX.DXGI`，用于 WGC 不可用场景（如部分 Win10 版本）；输出与 WGC 一致的帧格式 |
| **#13** | 帧格式统一化 | WGC 输出 `IDXGISurface` → 读取为 `byte[]` (BGRA)；统一转换为 OpenCV `Mat` (BGR)；处理旋转（`RotationMode`） |
| **#14** | 后端自动选择 `CaptureBackendFactory` | 运行时检测系统能力，优先 WGC，失败回退 DXGI；记录选择原因到日志 |
| **#15** | 捕获性能监控 | 实时统计：实际 FPS、丢帧数、CPU/GPU 占用、帧间隔 P50/P95/P99；通过 `IFrameMetrics` 暴露 |
| **#16** | 捕获压力测试 | 录制 5 分钟 1080p@60FPS，CPU 占用 < 15%（1080p 桌面），无丢帧；输出性能报告 |

**技术要点**：
- WGC 必须用 `DispatcherQueue`（WinRT 调度器），建议用 `Microsoft.Windows.System.DispatcherQueueController.CreateOnCurrentThread()`
- DXGI 需要 D3D11 Device 复用，避免每帧重建
- HDR 场景需注意颜色空间转换（暂可仅支持 SDR，M3 再处理 HDR）
- 帧数据用 `Span<byte>` + `MemoryMarshal` 避免拷贝

---

### M1.3 — 录制流水线（5–6 天）

| Issue | 标题 | 验收标准 |
|-------|------|----------|
| **#17** | 录制会话模型 `RecordingSession` | 包含 `SessionId / StartTime / WindowInfo / Settings / Status (Idle/Recording/Paused/Stopped)`；JSON 可序列化 |
| **#18** | 帧环形缓冲区 `FrameRingBuffer` | `Channel<CapturedFrame>` 或 `RingBuffer<byte[]>`，容量可配置（默认 120 帧），背压策略：丢弃最旧或阻塞 |
| **#19** | 原始帧序列写入器 `RawFrameWriter` | 按 `{sessionId}/frames/{frameIndex:000000}.png` 命名写入；支持 PNG（无损）/ JPG（Q=95）可配置；写入异步，不阻塞捕获线程 |
| **#20** | 视频编码器 `VideoEncoder` | 基于 FFmpeg（`FFmpeg.AutoGen` 或 `FFMediaToolkit`），H.264 + AAC（可选音频）；支持硬件加速（NVENC/QSV/AMF）自动探测；输出 `.mp4` |
| **#21** | 录制控制器 `RecordingController` | 编排：捕获 → 缓冲 → 编码/落盘；提供 `Start/Stop/Pause/Resume`；停止时确保所有帧 flush |
| **#22** | 会话元数据 sidecar | 每个 session 目录生成 `session.json`：窗口信息、时长、帧数、FPS、编码参数、Git commit hash |
| **#23** | 录制集成测试 | 端到端：启动金铲铲 → 运行 `RecordingController` 30 秒 → 停止 → 验证：视频可播放 + 帧序列完整 + 元数据正确 |

**技术要点**：
- 编码器与捕获解耦：捕获线程只负责入队，编码线程消费
- 帧索引必须单调递增，便于后续按帧检索
- 视频与帧序列可二选一或同时输出（开关控制）
- 磁盘 IO 监控：写入速度跟不上时报警

---

### M1.4 — 数据采样与数据集组织（3–4 天）

| Issue | 标题 | 验收标准 |
|-------|------|----------|
| **#24** | 采样策略 `ISamplingStrategy` | 接口：`ShouldKeep(frame, timestamp) -> bool`；内置实现：`EveryNthFrame(n)` / `KeyframeOnly` / `TimeBased(fps)` / `ChangeDetection(threshold)` |
| **#25** | TFT 节奏感知采样（可选增强） | 阶段切换检测（基于像素差/直方图变化）自动提高采样率；非战斗阶段降采样 |
| **#26** | 数据集目录规范 | 固定结构：<br>`datasets/{date}/{sessionId}/{frames,video,meta}`<br>`datasets/manifest.jsonl`（每行一个 session 摘要） |
| **#27** | 数据集校验工具 `DatasetValidator` | 扫描数据集目录，验证：帧连续性、视频可解码、元数据完整、生成 `validation_report.md` |
| **#28** | 数据集统计 `DatasetStats` | 输出：总 session 数、总时长、平均时长、帧数分布、按小时/天的分布图（可选） |

**推荐目录结构**：
```
datasets/
├── 2025-01-15/
│   ├── session_20250115_143022_abc123/
│   │   ├── frames/
│   │   │   ├── 000000.png
│   │   │   ├── 000001.png
│   │   │   └── ...
│   │   ├── video.mp4
│   │   └── session.json
│   └── session_20250115_151530_def456/
│       └── ...
└── manifest.jsonl
```

---

### M1.5 — 用户界面（3–4 天）

| Issue | 标题 | 验收标准 |
|-------|------|----------|
| **#29** | CLI 命令 `chansight record` | 参数：`--window "金铲铲" --output ./datasets --fps 30 --format both --duration 1800`；支持 `--list-windows` 调试 |
| **#30** | 实时状态输出 | 录制中 CLI 输出：已录制时长、帧数、磁盘占用、当前 FPS；支持 `--quiet` |
| **#31** | 极简 GUI（WPF 或 Avalonia） | 窗口列表下拉框 + 开始/停止按钮 + 实时预览（缩略图 5FPS）+ 状态栏；可关闭 GUI 仅用 CLI |
| **#32** | 全局热键 | F9 开始/停止、F10 暂停；录制中不抢游戏焦点 |
| **#33** | 系统托盘 | 托盘图标 + 右键菜单（开始/停止/打开数据集目录/退出） |

**技术建议**：
- GUI 用 **Avalonia 11**（跨平台、现代化），或 WPF（仅 Windows、更成熟）
- 预览缩略图用 OpenCV `Cv2.Resize` + `Bitmap` 转换，避免阻塞捕获线程
- 托盘用 `Hardcodet.NotifyIcon.Wpf` 或 Avalonia 等价物

---

### M1.6 — 质量保障（贯穿，2 天）

| Issue | 标题 | 验收标准 |
|-------|------|----------|
| **#34** | 单元测试覆盖 | Core / Capture / Dataset 模块测试覆盖率 > 70%；关键路径 100% |
| **#35** | 帧内容校验 | 录制结束后随机抽 10 帧，与游戏窗口截图对比，SSIM > 0.99 |
| **#36** | 错误处理与恢复 | 录制中窗口关闭 → 自动停止并保存；磁盘满 → 优雅报错；编码器崩溃 → 回退到仅帧序列 |
| **#37** | 用户文档 | `docs/recording-guide.md`：如何启动金铲铲、如何录制、参数调优、常见问题 |

---

## 四、M1 关键验收标准（DoD — Definition of Done）

M1 完成的标志是用户可以执行以下流程并成功：

```bash
# 1. 启动金铲铲并进入对局
# 2. 在终端运行：
chansight record --window "金铲铲之战" --output ./datasets --fps 30 --format both

# 3. CLI 显示：
[INFO] Detected window: 金铲铲之战 (PID: 12345, 1920x1080)
[INFO] Backend: WGC (Windows.Graphics.Capture)
[INFO] Recording started. Press F9 to stop.
[INFO] 00:01:23 | Frames: 2490 | FPS: 30.1 | Disk: 156 MB | Dropped: 0

# 4. F9 停止后：
[INFO] Recording stopped. Session saved to: datasets/2025-01-15/session_xxx/
[INFO] Video: video.mp4 (45 MB, 30s, H.264)
[INFO] Frames: 900 files (PNG, 1920x1080)
[INFO] Manifest updated.
```

**M1 完成的硬性指标**：
- ✅ 录制 1 局完整对局（15–30 分钟）无崩溃
- ✅ 视频文件可用 VLC/FFmpeg 正常播放
- ✅ 帧序列完整、无丢失
- ✅ 元数据 JSON 包含所有必要字段
- ✅ 实际 FPS ≥ 设置 FPS 的 95%
- ✅ CPU 占用 < 20%（1080p@30FPS）
- ✅ 提供 ≥ 10 局真实对局数据集作为 M2 启动物料

---

## 五、关键技术建议与风险

### 1. 依赖选型推荐

| 用途 | 推荐库 | 备选 |
|------|--------|------|
| Win32 P/Invoke | `Vanara.PInvoke` | 手写 P/Invoke |
| WGC | `Microsoft.Windows.SDK.Contracts` + `WinRT.Runtime` | `ScreenCapture.NET` |
| DXGI/D3D11 | `Vortice.Windows` | `SharpDX`（已停维护） |
| OpenCV | `OpenCvSharp4` | — |
| 视频编码 | `FFMediaToolkit`（高层）或 `FFmpeg.AutoGen`（底层） | `MediaFoundation` |
| 日志 | `Serilog` | `NLog` |
| CLI | `System.CommandLine` | `Spectre.Console.Cli` |
| GUI | `Avalonia 11` | WPF |
| 测试 | `xUnit` + `FluentAssertions` | `NUnit` |

### 2. 性能关键路径

```
[捕获线程] → [环形缓冲] → [采样过滤] → [编码线程] → [磁盘]
     ↓
   [统计/监控]
```

- 捕获线程**绝不阻塞**，所有耗时操作异步
- 编码用独立线程池，CPU 密集
- 帧写入用 `Channel` + 批量 flush，减少 IO 次数

### 3. 已知风险与对策

| 风险 | 影响 | 对策 |
|------|------|------|
| 金铲铲使用反作弊保护，截屏被屏蔽 | 致命 | 优先测试 WGC（基于系统级合成，不被多数反作弊拦截）；准备 DXGI 备选；预留 `SendInput` 模拟截屏键的 fallback |
| WGC 在某些 Win10 版本不可用 | 高 | 自动回退 DXGI；在 README 标注最低系统要求 Win10 1903+ |
| 长时间录制磁盘占满 | 中 | 录制前检查可用空间；支持自动分割（按大小/时长） |
| HDR 颜色空间导致 OpenCV 处理异常 | 中 | M1 仅支持 SDR；HDR 留到 M3 |
| 多显示器不同 DPI | 中 | 强制 PerMonitorV2；窗口绑定使用物理像素 |

### 4. 反作弊兼容性验证（**M1 第一天就要做**）

```csharp
// 验证脚本：尝试捕获金铲铲窗口并保存一帧
var matcher = new TftWindowMatcher();
var window = matcher.FindFirst();
var backend = CaptureBackendFactory.Create(window);
backend.Start();
await Task.Delay(1000);
var frame = await backend.CaptureOnce();
frame.Save("tft_test.png");
backend.Stop();
// 人工肉眼检查 tft_test.png 是否为黑屏/遮挡
```

**如果 WGC 被屏蔽**：立即评估 DXGI；若 DXGI 也被屏蔽，需考虑：
- 使用 `PrintWindow` API（部分游戏可绕过）
- 驱动级捕获（`Mirror Driver`，复杂度高）
- 与用户协商：是否接受在训练/观战模式录制

---

## 六、后续阶段预览（M2–M5 概要）

为保证 M1 交付物可被后续阶段直接消费，M1 的数据结构需预留扩展点：

### M2 — 视觉预处理（消费 M1 数据）
- 棋盘 ROI 自动定位（基于 M1 录制的 100+ 帧标注）
- 透视变换矩阵计算
- 棋子/装备模板库构建工具（从 M1 帧序列截取）

### M3 — 状态识别
- 棋子识别（训练数据来源：M1 帧序列 + 标注工具）
- OCR 数字识别（金币、等级、倒计时）
- 装备图标匹配

### M4 — 状态机与策略
- 阶段识别（基于 M1 元数据中的时间戳 + 视觉信号）
- 经济计算器
- 阵容推荐引擎

### M5 — 实时辅助
- 端到端流水线（捕获 → 识别 → 推荐 → 叠加）
- OSD 叠加层（透明窗口覆盖在游戏上）
- 延迟优化（< 200ms）

---

## 七、立即可执行的下一步

**今天就可以开的 Issue**：
1. **#1** 解决方案脚手架（半天）
2. **#5** 窗口枚举工具（1 天）
3. **#6** 金铲铲窗口识别器（半天）
4. **#11** WGC 捕获最小验证（1 天）—— **关键风险验证，必须尽早做**

**建议的 Issue 依赖图**（M1 部分）：
```
#1 → #2, #3 → #5 → #6 → #7 → #8
                          ↓
              #10 → #11 → #13 → #14
                  → #12 ↗
                          ↓
                  #18 → #19, #20 → #21 → #22
                          ↓
                  #24 → #26 → #27
                          ↓
                  #29 → #31 → #32
```

---

## 总结

通过这次重构，M1 从"模糊的基础设施"变成了**一个用户当天就能上手录制对局的具体工具**。每个 Issue 都有明确的产出物和验收标准，且整体设计为 M2–M5 提供了**真实数据底座**和**清晰的接口边界**。

**最重要的提醒**：**#11（WGC 捕获验证）和 #6（金铲铲窗口识别）必须作为 M1 的第 0 天任务**，如果反作弊屏蔽了屏幕捕获，整个项目路径需要重新评估——不要等到第 2 周才发现这个问题。

需要我进一步展开某个具体 Issue 的实现细节、代码骨架或测试用例吗？

---

## Qwen 3.7 Flash ($(@{name=Qwen 3.7 Flash; id=qwen/qwen3.7-flash; outFile=docs/raw_responses/out_refine_qw.json}.id))

基于你的核心诉求，【铲瞳 (ChanSight)】的路线图已从“宏观功能堆砌”重构为**“数据底座优先 → 离线分析验证 → 模型集成 → 端到端实时”**的敏捷交付路径。以下提供细化后的 Milestone 规划、GitHub Issue 清单、验收标准与技术建议，完全对齐 `C# (.NET 8/9) + WGC/DXGI + OpenCvSharp4` 技术栈。

---
## 🎯 M1 核心定位：实机屏幕捕获与数据采样工具（MVP）
**周期预估**：2~3 周  
**交付物**：可独立运行的桌面应用，支持自动绑定金铲铲窗口、稳定采集帧序列、无损落盘、输出标准化数据集结构，**零 AI 依赖，纯工程验证**。

### 📋 GitHub Issues 任务清单（M1）

| Issue ID | 标题 | 模块 | 技术要点 | 验收标准 (AC) |
|:---|:---|:---|:---|:---|
| `M1-01` | `[Arch]` 项目骨架与依赖注入配置 | 架构 | `.NET 8` Solution 划分 (`Core`, `Capture`, `Recorder`, `UI`, `Data`)；`Microsoft.Extensions.DependencyInjection` + `Options` 模式；结构化日志 (Serilog) | ✅ 编译通过<br>✅ 配置项可热重载<br>✅ DI 容器初始化无循环依赖<br>✅ 日志按级别输出至文件与控制台 |
| `M1-02` | `[Window]` 目标进程/窗口发现与绑定服务 | 捕获 | `EnumWindows` + `GetWindowText` + `Process.GetProcessesByName`；支持模拟器多开识别；窗口句柄缓存与防泄漏 | ✅ 精准匹配金铲铲窗口标题/进程名<br>✅ 支持拖拽绑定或下拉选择<br>✅ 窗口最小化/关闭时优雅降级不崩溃 |
| `M1-03` | `[Capture]` WGC 高精度帧捕获管线 | 核心 | `Windows.Graphics.Capture.Direct3D11CaptureFramePool`；帧对象池复用；CPU/GPU 内存零拷贝转换；60/120 FPS 自适应 | ✅ 延迟 ≤ 30ms (Win10 1903+)<br>✅ 连续运行 2h 内存增长 < 50MB<br>✅ 支持分辨率动态适配 |
| `M1-04` | `[Capture]` DXGI 降级兼容方案 | 核心 | `IDXGIOutputDuplication` 实现；适用于旧系统或特殊反作弊环境；与 WGC 共享统一 `IFrameProvider` 接口 | ✅ 切换后 API 行为一致<br>✅ 帧率波动 ≤ ±5%<br>✅ 异常时自动回退并记录日志 |
| `M1-05` | `[Recorder]` 异步帧队列与磁盘写入引擎 | 录制 | `System.Threading.Channels` 构建生产者-消费者管道；H.264/H.265 硬件编码 (NVENC/AMF) 或 无损 PNG 序列直出；断点续录 | ✅ 丢帧率 < 1% (满载下)<br>✅ 视频/帧文件完整可播放<br>✅ 支持按对局自动命名 (`match_{timestamp}.mp4`) |
| `M1-06` | `[Data]` 帧采样策略与元数据日志器 | 数据 | 固定 ROI 裁剪 (棋盘/卡牌区域)；时间戳对齐；JSONL 格式输出 (`frame_id`, `ts`, `fps`, `roi_bbox`, `file_path`)；OpenCvSharp4 非阻塞调用 | ✅ 采样间隔可配 (1/3/5 fps)<br>✅ 元数据与帧文件严格对应<br>✅ 预处理线程不阻塞主捕获流 |
| `M1-07` | `[UI]` 极简控制面板与状态看板 | 交互 | WPF/CommunityToolkit.Mvvm；Start/Stop/Pause；实时 FPS、丢帧率、磁盘占用指示；窗口选择器；日志滚动面板 | ✅ 界面响应 < 100ms<br>✅ 状态指示准确同步后台<br>✅ 支持快捷键控制 (如 `Ctrl+Shift+R`) |
| `M1-08` | `[Test]` 自动化压力测试与验证脚本 | 质量 | xUnit/NUnit；模拟窗口注入；30 分钟连续录制压测；性能 Profiler 集成 (BenchmarkDotNet) | ✅ 通过率 100%<br>✅ 生成测试报告 (PDF/HTML)<br>✅ CI 流水线可跑通 |

---
## 📈 后续里程碑规划（M2~M5）简述

| 阶段 | 核心目标 | 关键交付 | 与 M1 衔接点 |
|:---|:---|:---|:---|
| **M2** | 离线数据清洗与可视化分析 | 帧序列比对工具、ROI 标注辅助、数据集导出脚本 (TFRecord/Parquet) | 复用 M1 的采样管线与元数据结构 |
| **M3** | 轻量级视觉推理沙盒 | ONNX Runtime 集成、模型加载器、批量推理批处理、结果 Overlay 渲染 | 提供标准化输入张量接口，对接 M1 输出的帧序列 |
| **M4** | 实时辅助决策引擎 | 低延迟推理管道、状态机解析、UI 提示层、配置热更新 | 将 M3 模型接入 M1 捕获流，实现 `Capture → Inference → Display` |
| **M5** | 端到端产品化 | 安装包打包、权限引导、模拟器兼容性优化、用户反馈收集 | 全链路打通，具备商用/开源发布条件 |

---
## ✅ 关键验收标准 (Acceptance Criteria) 汇总

| 维度 | 指标要求 | 测量方式 |
|:---|:---|:---|
| **捕获稳定性** | 连续运行 ≥ 4 小时无 OOM / 句柄泄漏 | 内存监视器 + 句柄计数监控 |
| **帧完整性** | 丢帧率 ≤ 1%，时间戳连续无跳变 | 元数据 JSONL 校验脚本 |
| **录制格式** | H.264 码率可控 / PNG 序列无损可解压 | `ffprobe` / `ImageMagick` 验证 |
| **采样精度** | ROI 坐标偏差 ≤ 2px，帧率误差 ≤ ±0.5 | 网格参考图标定测试 |
| **UI 响应** | 按钮点击延迟 ≤ 100ms，状态刷新 ≤ 500ms | 性能 Profiler + 人工计时 |
| **数据集可用** | 输出目录符合 `dataset/{match_id}/frames/*.png` + `meta.jsonl` | 自动化目录结构检查脚本 |

---
## 💡 技术栈落地建议与避坑指南

1. **WGC 优先，DXGI 兜底**  
   WGC 是微软官方推荐的高性能捕获 API，支持 Direct3D11 帧池与硬件加速。DXGI 仅作为 Win7/部分虚拟机环境的兼容层。两者应抽象为统一的 `ICaptureProvider` 接口。

2. **OpenCvSharp4 使用红线**  
   - ❌ 禁止在主捕获循环中同步调用 `cv::Mat` 转换或复杂算法（会阻塞帧池）。
   - ✅ 仅在 `Channel.Writer` 下游的独立工作线程中执行 ROI 裁剪、格式转换（BGRA→BGR）、基础预处理。
   - ✅ 使用 `Mat` 对象池 (`ObjectPool<Mat>`) 避免频繁 GC。

3. **录制格式选型策略**  
   - **训练集首选**：无损 PNG/JPEG 序列 + JSONL 元数据。便于后期重采样、增强、打标签。
   - **日常存档**：H.264/H.265 硬解编码（推荐 `SharpDX.MediaFoundation` 或封装 `FFmpeg.AutoGen`），码率控制在 8~15 Mbps，平衡体积与画质。

4. **模拟器兼容性处理**  
   金铲铲通常运行在 MuMu/LDPlayer 等模拟器上。需特别注意：
   - 窗口标题可能含随机后缀或隐藏字符 → 使用正则模糊匹配 + 进程 PID 双重校验。
   - 模拟器常开启“独占全屏”或“硬件加速” → 捕获前检测并提示切换为“窗口化/无边框”。

5. **.NET 8/9 特性利用**  
   - 使用 `System.Threading.Channels` 替代传统 `BlockingCollection`，提升高并发帧吞吐。
   - 配置管理采用 `IOptionsSnapshot` 实现运行时热更新。
   - 日志采用 `ILogger<T>` 配合 Serilog  sinks，便于后期接入 ELK/Grafana。

---
## 🛠️ GitHub 协作规范建议

```yaml
# .github/workflows/ci.yml (示例)
name: M1 CI
on: [push, pull_request]
jobs:
  build-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet restore && dotnet build --configuration Release
      - run: dotnet test --no-build --verbosity normal
      - name: Upload Artifacts
        uses: actions/upload-artifact@v4
        with: { name: chansight-m1-release, path: 'bin/Release/net8.0-windows/' }
```

- **Labels 体系**：`M1-Capture`, `M1-Recorder`, `M1-Data`, `bug`, `enhancement`, `good-first-issue`
- **PR 规范**：必须关联 Issue，包含本地压测截图/日志片段，CI 绿灯方可合并。
- **分支策略**：`main` → `dev` → `feature/m1-capture` → PR → `dev` → `release/m1` → `main`

---
## 🚀 下一步行动建议
1. 立即创建 `M1-CORE` 仓库标签与 Issue 模板。
2. 优先合并 `M1-01` (架构) 与 `M1-03` (WGC 捕获)，跑通 `窗口绑定 → 帧获取 → 内存缓冲` 最小闭环。
3. 准备一套标准测试对局视频（含开局、运营、战斗、结算），用于 M1 验收基准对比。

此规划确保 **第 2 周即可产出可实机录制的工具**，团队能立刻开始采集真实对局数据，同时为后续视觉模型训练打下标准化数据底座。如需某 Issue 的详细设计文档（如 WGC 帧池实现代码骨架、JSONL 元数据 Schema），可提供专项展开。

---
