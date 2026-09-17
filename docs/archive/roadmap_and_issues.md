# 【铲瞳 (ChanSight)】重构后分阶段路线图与 GitHub Issues 清单

## 1. 重构设计原则：【先采数据后建模，首阶段优先交付实机录制工具】
- **核心诉求**：打破“直到第5阶段才能端到端尝试”的传统慢周期，在 **Milestone 1 即交付一个独立可用、高性能的屏幕捕获与对局录制采样工具 (ChanSight.Recorder)**。
- **业务价值**：用户在第一周即可使用该工具绑定金铲铲游戏窗口，实时录制、采样并输出高质量的对局帧序列与数据集，为后续视觉检测与策略推演提供最真实的数据底座。

---

## 2. 里程碑规划矩阵

```
铲瞳 (ChanSight) 演进路线
├── 🚩 Milestone 1: 屏幕捕获与对局录制采样工具 (优先交付，即可实机录局产出数据)
├── 🚩 Milestone 2: 数据集标注、预处理与 4x7 棋盘/商店 ROI 定位
├── 🚩 Milestone 3: 视觉模型推理管线 (YOLOv11n 弈子/装备 + PaddleOCR 商店)
├── 🚩 Milestone 4: 局内状态机、牌库时序与超几何分布决策推演
└── 🚩 Milestone 5: Avalonia 置顶透明 Overlay 与 Native AOT 正式打包
```

---

## 3. GitHub Issues 详细任务清单 (按优先级精细拆解)

### 🚩 Milestone 1: 屏幕捕获与对局录制采样工具 (Priority: Immediate)

#### Issue #1: [Core] 搭建 C# (.NET 8/9) 多工程解决方案与基础框架
- **标签**: `infrastructure`, `M1`
- **任务清单**:
  - [ ] 创建 `ChanSight.sln`
  - [ ] 划分工程：`ChanSight.Core` (核心抽象)、`ChanSight.Capture` (捕获管道)、`ChanSight.Recorder` (录制与数据管道)、`ChanSight.Cli` (命令行录制工具)、`ChanSight.Tests`
  - [ ] 配置 `Directory.Build.props`（统一 Nullable, C# 12/13, 性能优化标志）
  - [ ] 引入基础 NuGet：`OpenCvSharp4`, `OpenCvSharp4.runtime.win`, `System.Threading.Channels`
- **验收标准**: `dotnet build` 一键编译成功，工程引用依赖无循环。

#### Issue #2: [Capture] 金铲铲/模拟器游戏窗口自动发现与 DPI 适配绑定
- **标签**: `capture`, `win32`, `M1`
- **任务清单**:
  - [ ] 基于 Win32 P/Invoke 实现 `WindowFinder`，支持模糊匹配“金铲铲之战”及各类模拟器标题
  - [ ] 支持根据进程名（PID/ProcessName）和窗口句柄（HWND）精准绑定
  - [ ] 处理 Windows Per-Monitor DPI 缩放，使用 `DwmGetWindowAttribute` 获取真实画面客户区尺寸（排除阴影边框）
  - [ ] 监听窗口生命周期（移动、尺寸缩放、最小化、关闭回调）
- **验收标准**: 启动后 1 秒内自动寻找到游戏窗口并获取绝对坐标与真实物理分辨率。

#### Issue #3: [Capture] Windows.Graphics.Capture (WGC) 硬件加速高速捕获引擎
- **标签**: `capture`, `gpu`, `M1`
- **任务清单**:
  - [ ] 封装 WinRT `GraphicsCaptureSession` 与 `Direct3D11CaptureFramePool`
  - [ ] 支持窗口内容直采（即使被其他窗口遮挡亦不影响捕获）
  - [ ] 实现帧率节流控制器（可自由配置 10 / 30 / 60 FPS）
  - [ ] 将捕获的 D3D11 Surface 转换为 BGR 格式的 `OpenCvSharp.Mat`
  - [ ] 引入 DXGI Desktop Duplication 作为向下兼容降级备选
- **验收标准**: 1080P 分辨率下稳定 30~60 FPS 连续截取，端到端捕获延迟 $< 5\text{ms}$，CPU 占用 $< 5\%$。

#### Issue #4: [Recorder] 异步高吞吐对局录制管道 (Frame Buffer & Video Writer)
- **标签**: `recorder`, `pipeline`, `M1`
- **任务清单**:
  - [ ] 基于 `System.Threading.Channels` 搭建异步生产者-消费者无锁队列（`DropOldest` 丢帧策略防内存积压）
  - [ ] 实现全局热键监听：`F6` 作为程序/录制的【启动/停止】全局开关
  - [ ] 实现 `VideoRecorderService`：支持一键开始、暂停、恢复、停止对局录制
  - [ ] 支持输出标准 MP4 / AVI 视频格式（OpenCV VideoWriter 编码）
  - [ ] 生成对局元数据元文件 `meta.json`（录制开始时间、帧率、总帧数、分辨率、对局时长）
- **验收标准**: 按下 `F6` 顺畅开始/停止录制，30 分钟对局视频无花屏/无断裂，内存占用稳定 $< 200\text{MB}$。

#### Issue #5: [Dataset] 对局数据集采样器 (按秒自动采样 + 自定义快照热键)
- **标签**: `dataset`, `tooling`, `M1`
- **任务清单**:
  - [ ] 实现定时快照采样器：按预设间隔（如每 2 秒 / 每 5 秒）提取单帧高清图片（PNG / JPG）
  - [ ] 实现单帧快照专用热键（如 `F7` / `F8`，避免与 `F6` 启停冲突）
  - [ ] 自动规范存储目录：`datasets/recordings/{session_id}/frames/{timestamp}_{index}.jpg`
- **验收标准**: 自动采样与手动单帧快照响应延迟 $< 50\text{ms}$，图片清晰无拉伸变形，可直接作为后续 YOLO/OCR 标注训练样本。

#### Issue #6: [CLI] 命令行交互录制工具与实机测试运行
- **标签**: `cli`, `ux`, `M1`
- **任务清单**:
  - [ ] 在 `ChanSight.Cli` 中实现交互式控制台菜单（列出窗口、开始录制、手动快照、实时 FPS 监控）
  - [ ] 接入 `Spectre.Console` 实现美观的实时帧率、磁盘写入速度与录制状态仪表盘
  - [ ] 编写使用说明文档与实机录制测试指南
- **验收标准**: 运行 CLI 命令即可一键启动录制并看到实时监控统计，对局结束后产出可播放视频与帧数据集。

---

### 🚩 Milestone 2: 数据集标注、预处理与 4x7 棋盘/商店 ROI 定位
- **Issue #7**: 对局采样帧数据清洗、图像去重（感知哈希）与标注格式转换（YOLO 格式）
- **Issue #8**: 1920x1080 标准坐标系下的 ROI 自适应映射器（棋盘、备战席、商店、金币、血量、对手栏）
- **Issue #9**: 棋盘 4x7 六角网格与备战席 9 格子区域自动切片流水线

### 🚩 Milestone 3: 视觉模型推理管线
- **Issue #10**: ONNX Runtime (DirectML) 跨平台 GPU 推理引擎集成
- **Issue #11**: YOLOv11n 弈子、星级与装备目标检测模块
- **Issue #12**: PaddleOCR-v4-Mobile 商店卡牌与数值识别（含赛季英雄字典剪枝）

### 🚩 Milestone 4: 局内状态机、牌库时序与超几何分布推演
- **Issue #13**: GameState 局内全状态机与 7 名对手侦察快照追踪
- **Issue #14**: 全局牌库时序追踪与超几何分布抽卡期望概率算法

### 🚩 Milestone 5: Avalonia 置顶透明 Overlay 与 Native AOT 发布
- **Issue #15**: Avalonia UI 11 跨平台置顶透明穿透悬浮窗 (Overlay HUD)
- **Issue #16**: 全链路端到端性能压测 (端到端 $\le 30\text{ms}$)
- **Issue #17**: .NET 8/9 Native AOT 单文件打包与跨平台分发

---

## 2. GitHub Issues 详细任务拆解清单

### 🚩 Milestone 1: 基础骨架与捕获管道 (Foundation & Capture)

#### Issue #1: [Core] 搭建 .NET 8/9 多项目分层解决方案骨架
- **标签 (Labels)**: `infrastructure`, `M1`
- **任务清单**:
  - [ ] 创建 `ChanSight.sln` 根解决方案
  - [ ] 创建类库子项目：`ChanSight.Core`、`ChanSight.Capture`、`ChanSight.Vision`、`ChanSight.Overlay` 与启动项目 `ChanSight.App`
  - [ ] 配置 `Directory.Build.props`（启用 Nullable、最新的 C# 语言特性、统一依赖版本）
  - [ ] 配置 `Native AOT` 兼容性检查标志
- **验收标准**: `dotnet build` 编译成功，项目间依赖关系清晰隔离。

#### Issue #2: [Capture] 实现 Windows.Graphics.Capture (WGC) 高速捕获管道
- **标签 (Labels)**: `capture`, `performance`, `M1`
- **任务清单**:
  - [ ] 基于 `Microsoft.Windows.CsWin32` / WinRT API 封装 `WgcCaptureService`
  - [ ] 实现目标窗口（金铲铲/模拟器）自动句柄查找与区域绑定
  - [ ] 搭建 `Direct3D11CaptureFramePool` 帧监听回路，直出 GPU 纹理
  - [ ] 封装 DXGI Desktop Duplication 作为向下兼容备选方案
  - [ ] 输出帧数据接入 `System.Threading.Channels` 有界管道 (`DropOldest` 丢帧防背压)
- **验收标准**: 1080P 分辨率下稳定 30~60 FPS 捕获，CPU 占用 $< 3\%$，端到端捕获延迟 $< 5\text{ms}$。

#### Issue #3: [Vision] 图像预处理与 MatPool 对象池封装
- **标签 (Labels)**: `vision`, `memory`, `M1`
- **任务清单**:
  - [ ] 引入 `OpenCvSharp4` 与平台 Native 依赖
  - [ ] 实现 `MatPool` 内存对象池，杜绝逐帧分配与 GC 抖动
  - [ ] 实现标准化预处理流水线：ROI 裁剪、Letterbox 保持宽高比缩放、BGR 转 RGB、归一化 Tensor 转换
- **验收标准**: 单帧预处理耗时 $< 2\text{ms}$，内存分配稳定无泄漏。

---

### 🚩 Milestone 2: 视觉推理与识别管线 (Vision & Inference)

#### Issue #4: [Vision] ONNX Runtime C# 与 DirectML GPU 加速引擎集成
- **标签 (Labels)**: `inference`, `gpu`, `M2`
- **任务清单**:
  - [ ] 引入 `Microsoft.ML.OnnxRuntime.DirectML`
  - [ ] 实现 `OnnxInferenceEngine` 单例管理与生命周期管控
  - [ ] 配置 DirectML 硬件加速提供者（跨 Intel/AMD/NVIDIA 显卡通用）与 CPU 自动降级
  - [ ] 实现 `OrtValue` 零拷贝张量映射
- **验收标准**: DirectML 正确识别 GPU 设备，空模型推理链路跑通且无内存泄漏。

#### Issue #5: [Vision] 棋盘 4x7 网格、弈子星级与装备 YOLOv11n 检测器
- **标签 (Labels)**: `model`, `vision`, `M2`
- **任务清单**:
  - [ ] 导出与加载 `yolov11n.onnx` 模型
  - [ ] 实现小目标（星级、装备图标）检测头后处理与 NMS (非极大值抑制)
  - [ ] 实现棋盘 4x7 六角网格与 9 格备战席的坐标投影算法
  - [ ] 输出强类型 `BoardDetectionResult` 实体
- **验收标准**: 单帧弈子与装备推理耗时 $< 8\text{ms}$，网格定位准确率 $> 98\%$。

#### Issue #6: [Vision] 商店 5 卡槽与金币/阶段 PaddleOCR 识别模块
- **标签 (Labels)**: `ocr`, `model`, `M2`
- **任务清单**:
  - [ ] 集成 `PaddleOCR-v4-Mobile` ONNX 文本检测与识别模型
  - [ ] 构建金铲铲 S 赛季官方英雄名称与羁绊字典过滤剪枝
  - [ ] 针对 5 格商店卡牌名称、金币数值、当前阶段 (如 2-1, 3-5) 进行并行局部裁剪识别
- **验收标准**: 5 张商店卡牌识别耗时 $< 10\text{ms}$，字典匹配纠错准确率 $> 99\%$。

---

### 🚩 Milestone 3: 状态推演与卡池算法 (Engine & Logic)

#### Issue #7: [Engine] 局内全状态机与 7 对手快照追踪器
- **标签 (Labels)**: `logic`, `state`, `M3`
- **任务清单**:
  - [ ] 构建 `GameState` 不可变快照实体（阶段、金币、等级、己方阵容/备战席、商店）
  - [ ] 实现对局时序监听器：识别观战切换并缓存 7 名对手的阵容与备战席快照
  - [ ] 使用不可变数据流 (`ImmutableArray`) 保证多线程并发读取安全
- **验收标准**: 状态切换无锁、无竞态条件，对手切屏侦察数据实时准确更新。

#### Issue #8: [Engine] 牌库历史追踪与超几何分布卡池概率算法
- **标签 (Labels)**: `math`, `algorithm`, `M3`
- **任务清单**:
  - [ ] 维护全局各费卡已消耗/已购买牌库时序状态
  - [ ] 引入 `MathNet.Numerics`，实现基于超几何分布的下一抽/D牌期望概率运算
  - [ ] 计算当前目标阵容卡牌的剩余卡池存量与同行竞争度
- **验收标准**: 概率推演单次计算耗时 $< 1\text{ms}$，数学期望值与理论公式一致。

---

### 🚩 Milestone 4: 跨平台 Avalonia 悬浮窗 (UI & Overlay)

#### Issue #9: [Overlay] Avalonia UI 11 置顶透明与鼠标穿透窗口
- **标签 (Labels)**: `ui`, `cross-platform`, `M4`
- **任务清单**:
  - [ ] 构建 Avalonia XAML 悬浮窗主界面
  - [ ] 设置 `TransparencyLevelHint="Transparent"` 与无边框窗口样式
  - [ ] 接入 Windows/macOS 原生平台事件，实现游戏操作鼠标穿透 (`WS_EX_TRANSPARENT`) 与快捷键交互唤起
- **验收标准**: 窗口完全贴合游戏层、无遮挡游戏点击，GPU 渲染开销 $< 2\text{ms}$。

#### Issue #10: [Overlay] 战况 HUD、推荐拿牌与对手阵容可视化组件
- **标签 (Labels)**: `ui`, `mvvm`, `M4`
- **任务清单**:
  - [ ] 基于 `CommunityToolkit.Mvvm` 编写 `OverlayViewModel`
  - [ ] 实现顶部信息栏（阶段/金币/血量/等级）
  - [ ] 实现右侧推荐拿牌/牌池概率面板
  - [ ] 实现左侧 7 名对手阵容与留牌雷达面板
- **验收标准**: 界面数据毫秒级响应后端管道，UI 动画流畅，帧率稳定 60 FPS。

---

### 🚩 Milestone 5: 全链路联调与 AOT 发布 (Integration & Release)

#### Issue #11: [Integration] 捕获-预处理-推理-决策-渲染全链路端到端压测
- **标签 (Labels)**: `integration`, `performance`, `M5`
- **任务清单**:
  - [ ] 串联完整数据流管道：Capture $\rightarrow$ Preprocess $\rightarrow$ Inference $\rightarrow$ State Engine $\rightarrow$ Overlay
  - [ ] 使用 `BenchmarkDotNet` 与性能打点分析全链路耗时
  - [ ] 优化瓶颈环节，确保总端到端延迟稳定 $\le 30\text{ms}$
- **验收标准**: 连续挂机运行 2 小时无内存泄漏，GC Gen0/1/2 暂停时间几乎为零。

#### Issue #12: [Release] Native AOT 单文件编译与跨平台打包分发
- **标签 (Labels)**: `release`, `aot`, `M5`
- **任务清单**:
  - [ ] 配置 `<PublishAot>true</PublishAot>` 与 NativeLibrary 解析器
  - [ ] 消除所有 AOT 裁剪警告与动态反射
  - [ ] 产出独立单文件 `.exe`（Windows）及对应跨平台产物
- **验收标准**: 单文件启动时间 $< 50\text{ms}$，分发体积 $< 60\text{MB}$，无需预装 .NET 运行时。
