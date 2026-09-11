# ChanSight - Milestones and GitHub Issues Breakdown Discussion

## DeepSeek V4 Flash ($(@{name=DeepSeek V4 Flash; id=deepseek/deepseek-v4-flash; outFile=docs/out_issues_ds.json}.id))

根据项目技术栈与目标，我们制定以下分阶段开发路线图（共5个里程碑），每个里程碑包含若干GitHub Issue，每个Issue附带详细的任务清单与验收标准。所有内容均采用Markdown格式，可直接导入GitHub Projects或作为Issue模板使用。

---

# 铲瞳 (ChanSight) 分阶段开发路线图

## 总览

| 里程碑 | 名称 | 预计周期 | 核心目标 |
|--------|------|----------|----------|
| M1 | 基础骨架与捕获管道 | 2周 | 项目初始化、屏幕捕获、图像预处理、帧通道传输 |
| M2 | 视觉推理与模型管线 | 3周 | ONNX Runtime集成、模型加载与推理、DirectML加速 |
| M3 | 状态推演与概率算法 | 2周 | 状态机设计、概率滤波/预测算法、结果缓存 |
| M4 | 跨平台Avalonia悬浮窗 | 3周 | 悬浮窗UI、跨平台适配、数据绑定与实时显示 |
| M5 | 闭环联调与AOT发布 | 2周 | 集成测试、性能优化、Native AOT编译、发布包制作 |

---

## M1: 基础骨架与捕获管道

### Issue #1.1: 项目脚手架与解决方案结构

- **模块**: 基础设施
- **核心任务清单**:
  - [ ] 使用 `dotnet new sln` 创建解决方案
  - [ ] 创建项目：`ChanSight.Capture` (类库)、`ChanSight.Core` (类库)、`ChanSight.UI` (Avalonia应用)
  - [ ] 配置 `.editorconfig`、`Directory.Build.props` 统一版本与警告
  - [ ] 引入NuGet包：`OpenCvSharp4`、`OpenCvSharp4.runtime.win`、`System.Threading.Channels`
  - [ ] 配置 `net8.0` 目标框架（后续升级至net9.0）
  - [ ] 添加 `CommunityToolkit.Mvvm` 到UI项目
  - [ ] 添加 `NativeAOT` 相关属性（`PublishAot=true`，但暂不发布）
- **交付物**: 可编译的解决方案，包含三个项目，无编译错误。
- **验收标准**: `dotnet build` 通过，所有NuGet包正确引用。

### Issue #1.2: 屏幕捕获管道 (Windows.Graphics.Capture)

- **模块**: Capture
- **核心任务清单**:
  - [ ] 实现 `ScreenCaptureService` 类，封装 `Windows.Graphics.Capture` API
  - [ ] 创建 `CaptureItem` 枚举（窗口/显示器/自定义区域）
  - [ ] 实现 `StartCapture(CaptureItem)` 方法，返回 `Channel<Direct3DSurface>`
  - [ ] 实现帧循环：使用 `Direct3D11CaptureFramePool` 与 `CaptureSession`
  - [ ] 将捕获到的 `Direct3DSurface` 转换为 `Mat` (OpenCvSharp4) 并写入Channel
  - [ ] 添加 `StopCapture()` 方法释放资源
  - [ ] 处理窗口关闭/显示器断开等异常
- **交付物**: `ScreenCaptureService` 类及单元测试（模拟捕获帧）
- **验收标准**: 能捕获当前窗口并输出连续帧到Channel，帧率≥30fps（1080p）

### Issue #1.3: 图像预处理管线 (OpenCvSharp4)

- **模块**: Core
- **核心任务清单**:
  - [ ] 实现 `FramePreprocessor` 类，接收 `Mat` 输入
  - [ ] 添加预处理步骤：缩放（保持宽高比）、颜色空间转换（BGR→RGB）、归一化
  - [ ] 支持可配置的预处理参数（通过 `PreprocessingOptions` 类）
  - [ ] 将预处理后的 `Mat` 转换为 `Tensor` 格式（float数组，NHWC或NCHW）
  - [ ] 使用 `Channel<PreprocessedFrame>` 输出预处理结果
  - [ ] 实现 `IDisposable` 模式管理OpenCV资源
- **交付物**: `FramePreprocessor` 类及预处理配置
- **验收标准**: 输入任意尺寸的Mat，输出符合ONNX模型输入要求的Tensor（例如224x224 RGB float）

### Issue #1.4: 帧通道调度与背压控制

- **模块**: Core
- **核心任务清单**:
  - [ ] 设计 `FramePipeline` 类，协调捕获→预处理→推理各阶段
  - [ ] 使用 `System.Threading.Channels` 创建有界通道（BoundedChannel），容量可配置
  - [ ] 实现背压策略：当下游处理慢时自动丢弃旧帧（`BoundedChannelFullMode.DropOldest`）
  - [ ] 添加 `PipelineMetrics` 记录帧率、丢帧数、处理延迟
  - [ ] 提供 `Start()` / `Stop()` 方法管理生命周期
- **交付物**: `FramePipeline` 类及性能指标接口
- **验收标准**: 在模拟高帧率输入时，通道不会无限增长，丢帧行为符合预期

---

## M2: 视觉推理与模型管线

### Issue #2.1: ONNX Runtime 集成与模型加载

- **模块**: Core
- **核心任务清单**:
  - [ ] 引入 `Microsoft.ML.OnnxRuntime` 与 `Microsoft.ML.OnnxRuntime.DirectML` NuGet包
  - [ ] 实现 `OnnxModelLoader` 类，支持从文件路径加载 `.onnx` 模型
  - [ ] 创建 `ModelMetadata` 记录输入/输出名称、形状、类型
  - [ ] 实现 `InferenceSession` 的创建与配置（`SessionOptions` 指定DirectML执行提供者）
  - [ ] 添加模型版本校验与错误处理
- **交付物**: `OnnxModelLoader` 类及加载示例模型（如yolov8n.onnx）
- **验收标准**: 能成功加载模型并获取输入输出元数据，DirectML提供者可用

### Issue #2.2: 推理执行器 (InferenceExecutor)

- **模块**: Core
- **核心任务清单**:
  - [ ] 实现 `InferenceExecutor` 类，接收 `PreprocessedFrame` 并执行推理
  - [ ] 将 `float[]` 张量数据填充到 `OrtValue` 中
  - [ ] 调用 `session.Run()` 获取输出 `OrtValue`
  - [ ] 支持异步推理（`Task.Run` 或自定义线程池）
  - [ ] 实现推理结果封装 `InferenceResult`（包含原始输出张量、推理耗时）
  - [ ] 使用 `Channel<InferenceResult>` 输出结果
- **交付物**: `InferenceExecutor` 类及性能基准测试
- **验收标准**: 推理延迟 < 50ms（使用DirectML，模型如yolov8n）

### Issue #2.3: 后处理与结果解析

- **模块**: Core
- **核心任务清单**:
  - [ ] 实现 `PostProcessor` 抽象基类，支持不同模型的后处理
  - [ ] 以YOLOv8为例：解析输出张量，应用NMS（非极大值抑制）
  - [ ] 使用 `OpenCvSharp4` 的 `Dnn.NMSBoxes` 或手动实现
  - [ ] 生成 `DetectionResult` 列表（包含类别、置信度、边界框）
  - [ ] 支持自定义后处理插件（通过依赖注入或工厂模式）
- **交付物**: `PostProcessor` 基类及YOLOv8实现
- **验收标准**: 对已知测试图像，后处理结果与官方参考一致

### Issue #2.4: DirectML 性能调优

- **模块**: Core
- **核心任务清单**:
  - [ ] 测试不同 `ExecutionMode`（ORT_SEQUENTIAL / ORT_PARALLEL）
  - [ ] 调整 `IntraOpNumThreads` 与 `InterOpNumThreads`
  - [ ] 使用 `DirectML` 的 `EnableGraphCapture` 选项（如支持）
  - [ ] 对比CPU与DirectML推理延迟，记录数据
  - [ ] 实现推理结果缓存（相同输入跳过推理，用于静态场景）
- **交付物**: 性能调优报告及最终配置参数
- **验收标准**: 在RTX 3060上推理延迟 < 20ms（yolov8n）

---

## M3: 状态推演与概率算法

### Issue #3.1: 目标状态跟踪器

- **模块**: Core
- **核心任务清单**:
  - [ ] 设计 `TrackedObject` 类（ID、位置、速度、置信度、历史轨迹）
  - [ ] 实现 `KalmanFilter` 类（线性卡尔曼滤波，支持2D位置+速度）
  - [ ] 实现 `ObjectTracker` 类，管理多个 `TrackedObject`
  - [ ] 关联检测结果：使用匈牙利算法或IoU匹配进行数据关联
  - [ ] 处理新目标出现、目标消失、ID切换等场景
  - [ ] 输出 `TrackedFrame` 包含当前帧所有跟踪对象
- **交付物**: `ObjectTracker` 类及单元测试（模拟连续帧）
- **验收标准**: 在简单场景下（匀速运动），跟踪ID稳定，位置预测误差 < 10像素

### Issue #3.2: 概率状态推演引擎

- **模块**: Core
- **核心任务清单**:
  - [ ] 实现 `StateEstimator` 类，基于贝叶斯滤波（如粒子滤波）进行状态推演
  - [ ] 支持多种运动模型（匀速、匀加速、CTRV）
  - [ ] 集成 `MathNet.Numerics` 或手动实现高斯分布计算
  - [ ] 提供 `Predict(TimeSpan dt)` 与 `Update(Measurement)` 接口
  - [ ] 输出状态概率分布（均值+协方差）
- **交付物**: `StateEstimator` 类及粒子滤波实现
- **验收标准**: 在噪声测量下，状态估计误差低于单纯卡尔曼滤波

### Issue #3.3: 异常行为检测（可选）

- **模块**: Core
- **核心任务清单**:
  - [ ] 定义行为规则（如速度突变、区域入侵、停留超时）
  - [ ] 实现 `BehaviorAnalyzer` 类，订阅 `TrackedFrame` 事件
  - [ ] 使用滑动窗口统计历史轨迹
  - [ ] 触发 `BehaviorAlert` 事件（包含时间、目标ID、行为类型）
  - [ ] 支持规则配置（通过JSON或代码）
- **交付物**: `BehaviorAnalyzer` 类及示例规则
- **验收标准**: 能检测到预设的异常行为并发出警报

---

## M4: 跨平台Avalonia悬浮窗

### Issue #4.1: Avalonia 应用骨架与悬浮窗

- **模块**: UI
- **核心任务清单**:
  - [ ] 创建Avalonia应用项目，引用 `ChanSight.Core`
  - [ ] 实现 `MainWindow` 作为悬浮窗（无边框、置顶、半透明）
  - [ ] 使用 `WindowState` 与 `Topmost` 属性
  - [ ] 添加拖动功能（`PointerPressed` + `BeginMoveDrag`）
  - [ ] 支持右键菜单（设置、退出）
  - [ ] 跨平台测试（Windows、Linux、macOS基础运行）
- **交付物**: 可拖动的半透明悬浮窗
- **验收标准**: 窗口可拖动、置顶、半透明，右键菜单正常

### Issue #4.2: 实时数据绑定与MVVM

- **模块**: UI
- **核心任务清单**:
  - [ ] 创建 `MainViewModel` 继承 `ObservableObject`
  - [ ] 添加属性：`FrameCount`、`FPS`、`DetectionResults`（ObservableCollection）
  - [ ] 实现 `CommunityToolkit.Mvvm` 的 `RelayCommand`（启动/停止捕获）
  - [ ] 使用 `IDispatcher` 或 `Avalonia.Threading` 更新UI（跨线程）
  - [ ] 绑定 `MainWindow` 的DataContext到 `MainViewModel`
- **交付物**: ViewModel与UI绑定，实时显示帧率与检测结果
- **验收标准**: 启动后UI自动更新，无跨线程异常

### Issue #4.3: 结果可视化（覆盖层）

- **模块**: UI
- **核心任务清单**:
  - [ ] 在悬浮窗上绘制检测框与标签（使用 `Canvas` 或 `DrawingContext`）
  - [ ] 实现 `OverlayRenderer` 类，接收 `TrackedFrame` 绘制
  - [ ] 支持透明度、颜色、字体配置
  - [ ] 实现缩放与平移（跟随窗口大小）
  - [ ] 可选：显示原始帧缩略图（性能考虑，可关闭）
- **交付物**: 悬浮窗上实时显示检测框与跟踪ID
- **验收标准**: 检测框位置与屏幕坐标对齐，帧率≥15fps

### Issue #4.4: 跨平台适配与打包

- **模块**: UI
- **核心任务清单**:
  - [ ] 测试Windows、Linux（Ubuntu 22.04）、macOS（Sonoma）下的窗口行为
  - [ ] 处理不同DPI缩放（`Avalonia` 自动处理，但需验证）
  - [ ] 添加Linux下的 `libOpenCvSharp4` 依赖（通过runtimes）
  - [ ] 创建 `dotnet publish` 脚本（针对各平台）
  - [ ] 制作安装包（Windows用NSIS或WiX，Linux用AppImage，macOS用DMG）
- **交付物**: 三个平台的发布包（至少Windows可用）
- **验收标准**: 在Windows 11上安装并运行，捕获与显示正常

---

## M5: 闭环联调与AOT发布

### Issue #5.1: 集成测试与端到端流程

- **模块**: 集成
- **核心任务清单**:
  - [ ] 编写集成测试：启动捕获→推理→跟踪→UI显示全链路
  - [ ] 使用 `Microsoft.Playwright` 或 `Avalonia.Headless` 模拟UI
  - [ ] 测试不同分辨率（1080p、4K）下的性能
  - [ ] 测试长时间运行（>1小时）稳定性（内存泄漏、帧率下降）
  - [ ] 修复集成中发现的问题
- **交付物**: 集成测试报告与修复记录
- **验收标准**: 连续运行1小时无崩溃，内存增长 < 100MB

### Issue #5.2: 性能优化与Profiling

- **模块**: 优化
- **核心任务清单**:
  - [ ] 使用 `dotnet-trace` 或 `PerfView` 分析CPU热点
  - [ ] 优化内存分配（对象池、Span、ArrayPool）
  - [ ] 减少GC压力（使用 `struct` 代替 `class` 在热路径）
  - [ ] 优化OpenCV操作（避免 `Mat.Clone`，使用 `Mat.CopyTo` 等）
  - [ ] 调整Channel容量与背压参数
- **交付物**: 性能优化报告（对比优化前后帧率与延迟）
- **验收标准**: 在主流配置（i7-12700 + RTX 3060）下，端到端延迟 < 100ms，帧率≥30fps

### Issue #5.3: Native AOT 编译与发布

- **模块**: 发布
- **核心任务清单**:
  - [ ] 启用 `PublishAot=true` 并配置 `IlcOptimizationPreference=Speed`
  - [ ] 处理AOT不兼容的API（如 `System.Reflection.Emit`、动态加载）
  - [ ] 使用 `DirectPInvoke` 或 `LibraryImport` 替换 `DllImport`
  - [ ] 确保 `OpenCvSharp4` 的Native库正确嵌入（通过 `NativeLibrary` 或手动复制）
  - [ ] 测试AOT编译后的可执行文件大小与启动速度
  - [ ] 创建发布脚本（`dotnet publish -r win-x64 -c Release`）
- **交付物**: 单文件可执行程序（Windows x64）
- **验收标准**: AOT编译成功，程序启动时间 < 2秒，功能与JIT版本一致

### Issue #5.4: 用户文档与错误处理

- **模块**: 文档
- **核心任务清单**:
  - [ ] 编写 `README.md`：项目简介、安装步骤、使用说明
  - [ ] 编写 `CONFIG.md`：配置参数说明（模型路径、预处理选项、跟踪参数）
  - [ ] 实现全局异常处理（`AppDomain.CurrentDomain.UnhandledException`）
  - [ ] 添加日志系统（`Serilog` 或 `Microsoft.Extensions.Logging`）
  - [ ] 创建 `CHANGELOG.md` 记录版本变更
- **交付物**: 完整的用户文档与日志系统
- **验收标准**: 新用户按照README能在10分钟内运行成功

---

## 附录：Task List 结构（可直接导入GitHub）

以下为每个Issue的Markdown checklist模板，复制到GitHub Issue正文即可自动渲染为任务列表。

```markdown
### Issue #1.1: 项目脚手架与解决方案结构

- [ ] 使用 `dotnet new sln` 创建解决方案
- [ ] 创建项目：`ChanSight.Capture`、`ChanSight.Core`、`ChanSight.UI`
- [ ] 配置 `.editorconfig`、`Directory.Build.props`
- [ ] 引入NuGet包：`OpenCvSharp4`、`OpenCvSharp4.runtime.win`、`System.Threading.Channels`
- [ ] 配置 `net8.0` 目标框架
- [ ] 添加 `CommunityToolkit.Mvvm` 到UI项目
- [ ] 添加 `NativeAOT` 相关属性（`PublishAot=true`）
- [ ] 确保 `dotnet build` 通过
```

其他Issue类似，只需替换标题与任务列表即可。

---

## 总结

本路线图覆盖了从基础设施到发布的全流程，每个里程碑聚焦一个核心能力，Issue粒度适中，便于并行开发与代码审查。建议使用GitHub Projects的“里程碑”视图跟踪进度，每个Issue分配负责人与标签（如 `capture`、`inference`、`ui`、`optimization`）。欢迎讨论组对里程碑顺序或任务拆分提出调整建议。

---

## MiniMax M3 ($(@{name=MiniMax M3; id=minimax/minimax-m3; outFile=docs/out_issues_mm.json}.id))

# 铲瞳 (ChanSight) — 分阶段开发路线图 & GitHub Issues 任务清单

> **项目代号**：ChanSight  
> **技术栈**：C# (.NET 8/9) · Avalonia UI 11 · ONNX Runtime (DirectML) · Windows.Graphics.Capture · OpenCvSharp4 · System.Threading.Channels · CommunityToolkit.Mvvm · Native AOT  
> **目标**：实时屏幕捕获 → 视觉推理 → 状态推演 → 概率计算 → 悬浮窗可视化 → 闭环决策辅助

---

## 🗺️ 总体路线图概览

```
M1 ──► M2 ──► M3 ──► M4 ──► M5
基础骨架    视觉推理    状态推演    悬浮窗呈现    闭环联调 & AOT
 (4 周)    (4 周)     (3 周)     (3 周)      (3 周)
   │         │          │          │            │
   └─────────┴──────────┴──────────┴────────────┘
              共计约 17 周 (≈ 4 个月)
```

| Milestone | 主题 | 周期 | 关键产出 |
|-----------|------|------|----------|
| **M1** | 基础骨架与捕获管道 | W01–W04 | 可运行的屏幕捕获 → 帧队列 → 落盘/预览 |
| **M2** | 视觉推理与模型管线 | W05–W08 | ONNX 模型加载 → 推理 → 检测结果输出 |
| **M3** | 状态推演与概率算法 | W09–W11 | 游戏状态机 → 概率引擎 → 决策建议 |
| **M4** | 跨平台 Avalonia 悬浮窗 | W12–W14 | 透明置顶悬浮窗 → HUD 渲染 → 设置面板 |
| **M5** | 闭环联调与 AOT 发布 | W15–W17 | 端到端联调 → AOT 编译 → 安装包发布 |

---

## 📦 M1 — 基础骨架与捕获管道

> **目标**：搭建可扩展的解决方案骨架，实现稳定、低延迟的屏幕捕获与帧数据通道。

### 🔧 M1-001 · 初始化解决方案与项目骨架

| 字段 | 内容 |
|------|------|
| **所属模块** | `00-bootstrap` |
| **优先级** | 🔴 P0 |
| **依赖** | — |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 创建 `ChanSight.sln` 解决方案
- [ ] 建立分层项目结构：
  - `ChanSight.Core`（领域模型、接口定义）
  - `ChanSight.Capture`（屏幕捕获）
  - `ChanSight.Vision`（视觉推理）
  - `ChanSight.Reasoning`（状态推演）
  - `ChanSight.UI`（Avalonia 界面）
  - `ChanSight.Host`（主程序入口）
  - `ChanSight.Tests.Unit` / `ChanSight.Tests.Integration`
- [ ] 配置 `Directory.Build.props`（统一 LangVersion、Nullable、TreatWarningsAsErrors）
- [ ] 配置 `global.json` 锁定 .NET SDK 版本
- [ ] 添加 `.editorconfig`、`analyzer.ruleset`
- [ ] 初始化 `README.md`、`LICENSE`、`CHANGELOG.md`

**交付物**
- 可编译的多项目解决方案
- 统一的代码规范与构建配置

**验收标准**
- `dotnet build` 在 CI 中零警告通过
- 所有项目启用 `<Nullable>enable</Nullable>` 与 `<ImplicitUsings>enable</ImplicitUsings>`

---

### 🔧 M1-002 · 配置 CI/CD 与开发环境

| 字段 | 内容 |
|------|------|
| **所属模块** | `00-bootstrap` |
| **优先级** | 🔴 P0 |
| **依赖** | M1-001 |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 创建 GitHub Actions 工作流：
  - `ci.yml`（PR 触发：build + test + format check）
  - `release.yml`（tag 触发：打包 + 发布）
- [ ] 配置 `dotnet format` 与 `dotnet test` 自动化
- [ ] 添加 Issue / PR 模板
- [ ] 配置分支保护规则（main / develop）
- [ ] 添加 `dependabot.yml` 自动依赖更新

**交付物**
- 完整的 CI/CD 流水线
- 标准化的协作模板

**验收标准**
- PR 提交后自动触发 build + test
- 覆盖率报告自动上传

---

### 🔧 M1-003 · 日志与配置基础设施

| 字段 | 内容 |
|------|------|
| **所属模块** | `01-infrastructure` |
| **优先级** | 🔴 P0 |
| **依赖** | M1-001 |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 集成 `Serilog` + `Serilog.Sinks.File` + `Serilog.Sinks.Console`
- [ ] 实现 `IAppConfig` 接口，支持 JSON 热加载
- [ ] 实现结构化日志（带 SessionId、FrameId、LatencyMs）
- [ ] 实现日志滚动（按日 / 按大小）
- [ ] 实现配置变更通知（`IObservable<AppConfig>`）

**交付物**
- `LoggerFactory` 统一入口
- `appsettings.json` 模板

**验收标准**
- 日志包含 TraceId，可关联一次完整推理链路
- 修改配置文件后无需重启即可生效

---

### 🔧 M1-004 · Windows.Graphics.Capture 屏幕捕获服务

| 字段 | 内容 |
|------|------|
| **所属模块** | `02-capture` |
| **优先级** | 🔴 P0 |
| **依赖** | M1-003 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 封装 `ICaptureService` 接口
- [ ] 实现 `WindowsGraphicsCaptureService`（基于 WinRT API）
- [ ] 实现显示器枚举与窗口枚举
- [ ] 实现捕获区域选择（矩形 ROI）
- [ ] 处理 DWM 缩放与多显示器 DPI
- [ ] 实现 `Direct3D11Device` 共享纹理管理
- [ ] 实现帧回调（`IFrameArrivedHandler`）

**交付物**
- `CaptureService` 可注入服务
- 捕获示例 Demo（保存为 PNG）

**验收标准**
- 1080p 捕获延迟 ≤ 16ms（60 FPS）
- 支持窗口捕获与显示器捕获两种模式
- 捕获过程 CPU 占用 < 5%

---

### 🔧 M1-005 · 基于 System.Threading.Channels 的帧管道

| 字段 | 内容 |
|------|------|
| **所属模块** | `02-capture` |
| **优先级** | 🔴 P0 |
| **依赖** | M1-004 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 定义 `Frame` 数据结构（含 Timestamp、FrameId、SharedTexture 句柄）
- [ ] 实现 `IFrameChannel`（基于 `Channel<Frame>`）
- [ ] 配置 `BoundedChannelOptions`（容量、FullMode = DropOldest）
- [ ] 实现生产者（Capture）→ 消费者（Vision）背压机制
- [ ] 实现帧丢弃策略（按时间戳跳过过期帧）
- [ ] 实现通道监控指标（深度、丢弃率）

**交付物**
- 高性能、无锁的帧数据通道
- 通道健康度指标

**验收标准**
- 持续 30 分钟 60FPS 捕获无内存泄漏
- 消费者阻塞时生产者不卡顿（背压生效）

---

### 🔧 M1-006 · 捕获区域选择 UI（WPF/Avalonia 临时）

| 字段 | 内容 |
|------|------|
| **所属模块** | `02-capture` |
| **优先级** | 🟡 P1 |
| **依赖** | M1-004 |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 实现全屏半透明遮罩
- [ ] 实现鼠标拖拽框选 ROI
- [ ] 实现 ROI 持久化到配置文件
- [ ] 实现 ROI 实时预览

**交付物**
- `RegionSelector` 工具

**验收标准**
- 可框选任意矩形区域并保存
- 多显示器环境下坐标正确

---

### 🔧 M1-007 · MVVM 基础设施（CommunityToolkit.Mvvm）

| 字段 | 内容 |
|------|------|
| **所属模块** | `01-infrastructure` |
| **优先级** | 🔴 P0 |
| **依赖** | M1-001 |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 定义 `ObservableObject` 基类
- [ ] 实现 `IRelayCommand` / `IAsyncRelayCommand` 封装
- [ ] 实现 `IDialogService`、`INavigationService` 接口
- [ ] 实现依赖注入容器（`Microsoft.Extensions.DependencyInjection`）
- [ ] 实现 `IHostedService` 生命周期管理

**交付物**
- 标准 MVVM 基类与命令封装
- DI 容器配置

**验收标准**
- ViewModel 可零样板代码实现属性通知
- DI 容器支持 Scoped / Singleton / Transient

---

### 🔧 M1-008 · 帧预处理管线（OpenCvSharp4）

| 字段 | 内容 |
|------|------|
| **所属模块** | `02-capture` |
| **优先级** | 🟡 P1 |
| **依赖** | M1-005 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 实现 `IFramePreprocessor` 接口
- [ ] 实现色彩空间转换（Bgra → Bgr / Rgb）
- [ ] 实现尺寸归一化（Letterbox / Stretch）
- [ ] 实现 GPU 加速的 resize（如可用）
- [ ] 实现预处理结果缓存（避免重复计算）

**交付物**
- 可插拔的预处理管线
- 性能基准测试报告

**验收标准**
- 1080p → 640x640 预处理耗时 ≤ 3ms
- 支持 CPU / GPU 双后端切换

---

### ✅ M1 验收标准（Milestone Gate）

- [ ] 屏幕捕获 → 帧通道 → 预处理 → 落盘全链路打通
- [ ] 单元测试覆盖率 ≥ 60%
- [ ] 持续运行 1 小时无内存泄漏（dotnet-trace 验证）
- [ ] CI 全绿，文档齐全

---

## 🧠 M2 — 视觉推理与模型管线

> **目标**：构建可热插拔的 ONNX 推理管线，支持目标检测、分类、OCR 等多模型协同。

### 🔧 M2-001 · ONNX Runtime + DirectML 集成

| 字段 | 内容 |
|------|------|
| **所属模块** | `03-vision` |
| **优先级** | 🔴 P0 |
| **依赖** | M1-005 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 集成 `Microsoft.ML.OnnxRuntime.DirectML`
- [ ] 实现 `IInferenceBackend` 抽象（CPU / DirectML / CUDA 可切换）
- [ ] 实现 `SessionOptions` 配置（GraphOptimizationLevel、ExecutionMode）
- [ ] 实现 `OrtIoBinding` 零拷贝输入输出
- [ ] 实现 GPU 设备枚举与选择
- [ ] 实现推理异常处理与降级

**交付物**
- `OnnxInferenceBackend` 实现
- 推理后端工厂

**验收标准**
- DirectML 推理 YOLOv8n 1080p 单帧 ≤ 25ms（GPU）
- CPU 推理可作为降级方案

---

### 🔧 M2-002 · 模型加载与版本管理

| 字段 | 内容 |
|------|------|
| **所属模块** | `03-vision` |
| **优先级** | 🔴 P0 |
| **依赖** | M2-001 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 定义 `ModelManifest`（Name、Version、Url、Sha256、InputShape）
- [ ] 实现 `IModelRegistry` 模型注册表
- [ ] 实现模型下载（带断点续传、校验）
- [ ] 实现模型本地缓存（`%APPDATA%/ChanSight/models/`）
- [ ] 实现模型版本回滚
- [ ] 实现模型签名校验（防篡改）

**交付物**
- 模型管理器
- 模型清单配置文件

**验收标准**
- 首次启动自动下载所需模型
- 模型损坏时自动重新下载

---

### 🔧 M2-003 · OpenCV 预处理管线（推理专用）

| 字段 | 内容 |
|------|------|
| **所属模块** | `03-vision` |
| **优先级** | 🔴 P0 |
| **依赖** | M2-001 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 实现 `IPreprocessor` 链式组合
- [ ] 实现 `LetterboxResize`（保持长宽比）
- [ ] 实现 `Normalize`（ImageNet 均值方差）
- [ ] 实现 `ChannelFirst`（HWC → CHW）
- [ ] 实现 `ToTensor`（Mat → OrtValue）
- [ ] 实现预处理性能分析（每步耗时）

**交付物**
- 可组合的预处理管线
- 性能分析报告

**验收标准**
- 预处理耗时 ≤ 2ms / 帧
- 与 Python 参考实现数值误差 < 1e-5

---

### 🔧 M2-004 · 目标检测模块（YOLO 系列）

| 字段 | 内容 |
|------|------|
| **所属模块** | `03-vision` |
| **优先级** | 🔴 P0 |
| **依赖** | M2-003 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 实现 `IDetector` 接口
- [ ] 实现 YOLOv8 / YOLOv10 后处理（NMS、置信度过滤）
- [ ] 实现 `Detection` 数据结构（BBox、ClassId、Confidence、ClassName）
- [ ] 实现多尺度检测（可选）
- [ ] 实现检测结果可视化（调试用）
- [ ] 实现检测结果缓存（短时帧间追踪）

**交付物**
- `YoloDetector` 实现
- 检测结果可视化工具

**验收标准**
- mAP50 与参考实现一致（误差 < 1%）
- 单帧检测延迟 ≤ 30ms（GPU）

---

### 🔧 M2-005 · OCR / 文本识别模块

| 字段 | 内容 |
|------|------|
| **所属模块** | `03-vision` |
| **优先级** | 🟡 P1 |
| **依赖** | M2-004 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 集成 PaddleOCR / EasyOCR ONNX 版本
- [ ] 实现文本检测（DBNet / EAST）
- [ ] 实现文本识别（CRNN / SVTR）
- [ ] 实现方向分类（可选）
- [ ] 实现文本框合并（行内合并）
- [ ] 实现多语言支持（中/英）

**交付物**
- `OcrService` 实现
- OCR 准确率测试集

**验收标准**
- 印刷体中文识别准确率 ≥ 95%
- 单帧 OCR 耗时 ≤ 50ms

---

### 🔧 M2-006 · 推理结果后处理与关联

| 字段 | 内容 |
|------|------|
| **所属模块** | `03-vision` |
| **优先级** | 🟡 P1 |
| **依赖** | M2-004, M2-005 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 实现 `IResultMerger` 多模型结果融合
- [ ] 实现帧间目标追踪（IoU Tracker / SORT）
- [ ] 实现目标 ID 稳定性（同目标跨帧 ID 一致）
- [ ] 实现置信度时间累积（避免抖动）
- [ ] 实现结果去重与平滑

**交付物**
- 结果融合器
- 追踪器实现

**验收标准**
- 目标追踪 ID 切换率 < 5%
- 抖动场景下输出稳定

---

### 🔧 M2-007 · 模型热插拔与降级策略

| 字段 | 内容 |
|------|------|
| **所属模块** | `03-vision` |
| **优先级** | 🟡 P1 |
| **依赖** | M2-002 |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 实现运行时模型替换（不重启）
- [ ] 实现模型加载失败回退（默认模型）
- [ ] 实现 GPU 不可用时自动切换 CPU
- [ ] 实现模型 A/B 测试框架

**交付物**
- 模型热更新机制

**验收标准**
- 模型替换期间推理不中断
- 异常降级有日志可追溯

---

### 🔧 M2-008 · 推理性能监控

| 字段 | 内容 |
|------|------|
| **所属模块** | `03-vision` |
| **优先级** | 🟢 P2 |
| **依赖** | M2-001 |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 实现推理耗时统计（P50 / P95 / P99）
- [ ] 实现 GPU 利用率采样
- [ ] 实现 FPS 计算
- [ ] 实现性能数据导出（Prometheus / CSV）

**交付物**
- 性能监控面板（可选 Avalonia 窗口）

**验收标准**
- 实时显示推理性能指标
- 性能数据可导出分析

---

### ✅ M2 验收标准

- [ ] 帧 → 预处理 → 推理 → 后处理 → 检测结果全链路打通
- [ ] 端到端推理延迟 ≤ 50ms（GPU）
- [ ] 支持至少 2 个模型协同工作
- [ ] 模型可热更新

---

## ♟️ M3 — 状态推演与概率算法

> **目标**：将视觉输出转化为游戏状态，并基于规则与概率模型生成决策建议。

### 🔧 M3-001 · 游戏状态数据模型

| 字段 | 内容 |
|------|------|
| **所属模块** | `04-reasoning` |
| **优先级** | 🔴 P0 |
| **依赖** | M2-006 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 定义 `GameState` 不可变记录
- [ ] 定义 `PlayerState`、`BoardState`、`DeckState` 等子结构
- [ ] 实现状态变更事件流（`IObservable<GameStateChange>`）
- [ ] 实现状态序列化 / 反序列化
- [ ] 实现状态快照与回放

**交付物**
- 领域模型库
- 状态变更事件总线

**验收标准**
- 状态模型覆盖所有游戏场景
- 状态变更可追溯、可回放

---

### 🔧 M3-002 · 卡牌 / 元素数据库

| 字段 | 内容 |
|------|------|
| **所属模块** | `04-reasoning` |
| **优先级** | 🔴 P0 |
| **依赖** | M3-001 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 设计卡牌元数据 Schema（Id、Name、Cost、Type、Rarity、Effect）
- [ ] 实现 `ICardDatabase` 接口
- [ ] 实现 SQLite / JSON 双后端
- [ ] 实现卡牌图片哈希匹配（视觉 → ID）
- [ ] 实现卡牌数据导入工具
- [ ] 实现版本化数据更新

**交付物**
- 卡牌数据库
- 数据导入 CLI 工具

**验收标准**
- 卡牌识别准确率 ≥ 98%
- 数据库支持热更新

---

### 🔧 M3-003 · 状态追踪器（历史窗口）

| 字段 | 内容 |
|------|------|
| **所属模块** | `04-reasoning` |
| **优先级** | 🔴 P0 |
| **依赖** | M3-001 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 实现 `IStateTracker` 接口
- [ ] 实现滑动窗口状态历史（最近 N 帧）
- [ ] 实现状态变化检测（Diff 算法）
- [ ] 实现未知状态回退（置信度低时保留旧值）
- [ ] 实现状态机校验（合法性检查）
- [ ] 实现异常状态恢复

**交付物**
- 状态追踪器
- 状态机校验规则

**验收标准**
- 状态变更延迟 ≤ 100ms
- 异常输入下不崩溃

---

### 🔧 M3-004 · 概率引擎（核心算法）

| 字段 | 内容 |
|------|------|
| **所属模块** | `04-reasoning` |
| **优先级** | 🔴 P0 |
| **依赖** | M3-002, M3-003 |
| **预估工时** | 4d |

**核心任务清单**
- [ ] 实现 `IProbabilityEngine` 接口
- [ ] 实现超几何分布计算（抽卡概率）
- [ ] 实现贝叶斯推断（对手手牌推测）
- [ ] 实现蒙特卡洛模拟（决策评估）
- [ ] 实现概率缓存（避免重复计算）
- [ ] 实现概率可视化数据结构

**交付物**
- 概率计算库
- 算法单元测试（与 Python 参考实现对比）

**验收标准**
- 概率计算结果与理论值误差 < 1e-6
- 蒙特卡洛 10000 次模拟 ≤ 200ms

---

### 🔧 M3-005 · 决策建议引擎

| 字段 | 内容 |
|------|------|
| **所属模块** | `04-reasoning` |
| **优先级** | 🟡 P1 |
| **依赖** | M3-004 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 定义 `IDecisionAdvisor` 接口
- [ ] 实现规则引擎（Drools / 自研 DSL）
- [ ] 实现效用函数（Utility Function）
- [ ] 实现多目标排序（胜率 / 期望收益 / 风险）
- [ ] 实现建议生成器（自然语言 + 数值）
- [ ] 实现建议置信度评估

**交付物**
- 决策引擎
- 规则配置 DSL

**验收标准**
- 决策建议生成延迟 ≤ 50ms
- 建议可解释（附带理由）

---

### 🔧 M3-006 · 游戏规则引擎

| 字段 | 内容 |
|------|------|
| **所属模块** | `04-reasoning` |
| **优先级** | 🟡 P1 |
| **依赖** | M3-001 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 实现 `IRuleEngine` 接口
- [ ] 实现规则定义 DSL（YAML / JSON）
- [ ] 实现规则优先级与冲突解决
- [ ] 实现规则热加载
- [ ] 实现规则调试器（断点、日志）

**交付物**
- 规则引擎
- 示例规则集

**验收标准**
- 规则变更无需重启
- 规则执行可追溯

---

### 🔧 M3-007 · 模拟框架（What-If 分析）

| 字段 | 内容 |
|------|------|
| **所属模块** | `04-reasoning` |
| **优先级** | 🟢 P2 |
| **依赖** | M3-004 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 实现 `ISimulator` 接口
- [ ] 实现回合制模拟器
- [ ] 实现分支探索（剪枝优化）
- [ ] 实现模拟结果聚合
- [ ] 实现模拟可视化

**交付物**
- 模拟器库
- 模拟结果分析工具

**验收标准**
- 1000 次模拟 ≤ 1s
- 模拟结果可重现（固定随机种子）

---

### 🔧 M3-008 · 状态验证与错误恢复

| 字段 | 内容 |
|------|------|
| **所属模块** | `04-reasoning` |
| **优先级** | 🟡 P1 |
| **依赖** | M3-003 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 实现状态一致性校验
- [ ] 实现异常状态自动修正
- [ ] 实现状态回滚机制
- [ ] 实现错误上报（带上下文）
- [ ] 实现用户手动校正入口

**交付物**
- 状态校验器
- 错误恢复策略

**验收标准**
- 异常状态自动恢复率 ≥ 90%
- 错误可追溯、可上报

---

### ✅ M3 验收标准

- [ ] 视觉输出 → 游戏状态 → 概率计算 → 决策建议全链路打通
- [ ] 概率算法与理论值一致
- [ ] 决策建议附带可解释理由
- [ ] 异常状态自动恢复

---

## 🪟 M4 — 跨平台 Avalonia 悬浮窗

> **目标**：构建透明、置顶、可交互的悬浮窗，跨平台呈现分析结果。

### 🔧 M4-001 · Avalonia UI 11 项目初始化

| 字段 | 内容 |
|------|------|
| **所属模块** | `05-ui` |
| **优先级** | 🔴 P0 |
| **依赖** | M1-007 |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 创建 `ChanSight.UI` 项目（net8.0-windows / net8.0）
- [ ] 配置 `Avalonia.Desktop` + `Avalonia.Themes.Fluent`
- [ ] 实现跨平台条件编译（`#if WINDOWS` / `#if LINUX`）
- [ ] 实现主题切换（深色 / 浅色）
- [ ] 实现资源字典（颜色、字体、图标）

**交付物**
- Avalonia 项目骨架
- 主题系统

**验收标准**
- 项目可在 Windows / Linux 构建
- 主题切换流畅

---

### 🔧 M4-002 · 透明置顶悬浮窗（Windows）

| 字段 | 内容 |
|------|------|
| **所属模块** | `05-ui` |
| **优先级** | 🔴 P0 |
| **依赖** | M4-001 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 实现 `Window` 透明背景（`TransparencyLevelHint = Transparent`）
- [ ] 实现窗口置顶（`Topmost = true`）
- [ ] 实现窗口无边框（`WindowState`、`ExtendClientAreaToDecorationsHint`）
- [ ] 实现点击穿透（`IsHitTestVisible = false`）
- [ ] 实现多显示器 DPI 自适应
- [ ] 实现窗口位置记忆

**交付物**
- `OverlayWindow` 控件

**验收标准**
- 窗口透明且不影响游戏操作
- 多显示器下位置正确

---

### 🔧 M4-003 · 跨平台窗口抽象层

| 字段 | 内容 |
|------|------|
| **所属模块** | `05-ui` |
| **优先级** | 🟡 P1 |
| **依赖** | M4-002 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 定义 `IOverlayWindow` 接口
- [ ] 实现 `WindowsOverlayWindow`（基于 Win32 API）
- [ ] 实现 `LinuxOverlayWindow`（基于 X11 / Wayland）
- [ ] 实现 `MacOSOverlayWindow`（基于 Cocoa，可选）
- [ ] 实现平台检测与自动选择

**交付物**
- 跨平台窗口抽象

**验收标准**
- 同一代码库可在多平台编译
- 平台特性差异有文档说明

---

### 🔧 M4-004 · 自定义渲染系统（GPU 加速）

| 字段 | 内容 |
|------|------|
| **所属模块** | `05-ui` |
| **优先级** | 🟡 P1 |
| **依赖** | M4-002 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 实现 `IRenderTarget` 接口
- [ ] 实现基于 `SkiaSharp` 的 2D 渲染
- [ ] 实现检测框绘制（带阴影、动画）
- [ ] 实现文本渲染（多语言、轮廓）
- [ ] 实现图标 / 图片渲染
- [ ] 实现渲染性能监控

**交付物**
- 自定义渲染管线
- 渲染示例

**验收标准**
- 60 FPS 渲染（100+ 元素）
- 渲染与游戏画面无明显延迟

---

### 🔧 M4-005 · HUD 组件库

| 字段 | 内容 |
|------|------|
| **所属模块** | `05-ui` |
| **优先级** | 🟡 P1 |
| **依赖** | M4-004 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 实现 `CardView`（卡牌可视化）
- [ ] 实现 `ProbabilityBar`（概率条）
- [ ] 实现 `SuggestionPanel`（建议面板）
- [ ] 实现 `Tooltip`（悬停提示）
- [ ] 实现 `MiniMap`（小地图）
- [ ] 实现组件动画（淡入淡出、缩放）

**交付物**
- HUD 组件库
- 组件使用示例

**验收标准**
- 组件可组合、可复用
- 动画流畅（60 FPS）

---

### 🔧 M4-006 · 设置面板

| 字段 | 内容 |
|------|------|
| **所属模块** | `05-ui` |
| **优先级** | 🟡 P1 |
| **依赖** | M4-005 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 实现主设置窗口（非透明）
- [ ] 实现捕获设置（区域、刷新率）
- [ ] 实现模型设置（选择、下载、删除）
- [ ] 实现显示设置（透明度、缩放、位置）
- [ ] 实现快捷键设置
- [ ] 实现设置导入 / 导出

**交付物**
- 设置面板 UI
- 设置持久化

**验收标准**
- 设置变更实时生效
- 设置可备份恢复

---

### 🔧 M4-007 · 主题与本地化

| 字段 | 内容 |
|------|------|
| **所属模块** | `05-ui` |
| **优先级** | 🟢 P2 |
| **依赖** | M4-001 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 实现主题系统（深色 / 浅色 / 自定义）
- [ ] 实现资源切换（颜色、字体、图标）
- [ ] 实现本地化框架（基于 .resx）
- [ ] 实现中 / 英 / 日多语言
- [ ] 实现运行时语言切换

**交付物**
- 主题系统
- 多语言资源

**验收标准**
- 主题切换无闪烁
- 语言切换实时生效

---

### 🔧 M4-008 · 快捷键与系统托盘

| 字段 | 内容 |
|------|------|
| **所属模块** | `05-ui` |
| **优先级** | 🟢 P2 |
| **依赖** | M4-006 |
| **预估工时** | 1d |

**核心任务清单**
- [ ] 实现全局快捷键（启动 / 暂停 / 截图）
- [ ] 实现系统托盘图标
- [ ] 实现托盘菜单（显示 / 隐藏 / 退出）
- [ ] 实现开机自启动（可选）

**交付物**
- 全局快捷键服务
- 托盘组件

**验收标准**
- 快捷键在游戏中可触发
- 托盘菜单功能完整

---

### ✅ M4 验收标准

- [ ] 透明悬浮窗在游戏中正常显示
- [ ] HUD 组件流畅渲染
- [ ] 设置面板功能完整
- [ ] 跨平台构建通过

---

## 🚀 M5 — 闭环联调与 AOT 发布

> **目标**：端到端联调、性能优化、Native AOT 编译、生产级发布。

### 🔧 M5-001 · 端到端集成测试

| 字段 | 内容 |
|------|------|
| **所属模块** | `06-integration` |
| **优先级** | 🔴 P0 |
| **依赖** | M1–M4 全部 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 设计 E2E 测试场景（覆盖典型游戏流程）
- [ ] 实现录制回放框架（输入录制 → 离线回放）
- [ ] 实现全链路集成测试
- [ ] 实现回归测试套件
- [ ] 实现 CI 中的 E2E 自动化

**交付物**
- E2E 测试套件
- 测试报告

**验收标准**
- 核心场景 100% 通过
- 回归测试零失败

---

### 🔧 M5-002 · 性能剖析与优化

| 字段 | 内容 |
|------|------|
| **所属模块** | `06-integration` |
| **优先级** | 🔴 P0 |
| **依赖** | M5-001 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 使用 `dotnet-trace` / `dotnet-counters` 剖析
- [ ] 识别热点路径（CPU / 内存 / GC）
- [ ] 优化关键路径（Span\<T\>、ArrayPool、对象池）
- [ ] 优化 GC（避免 LOH 分配）
- [ ] 优化线程模型（避免锁竞争）
- [ ] 实现性能基准测试（基准对比）

**交付物**
- 性能优化报告
- 基准测试套件

**验收标准**
- 端到端延迟 ≤ 100ms（P95）
- 内存占用 ≤ 500MB
- CPU 占用 ≤ 15%（1080p 60FPS）

---

### 🔧 M5-003 · 内存泄漏检测与修复

| 字段 | 内容 |
|------|------|
| **所属模块** | `06-integration` |
| **优先级** | 🔴 P0 |
| **依赖** | M5-002 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 使用 `dotnet-dump` 分析堆内存
- [ ] 实现长时间运行测试（24h soak test）
- [ ] 修复发现的内存泄漏
- [ ] 实现 IDisposable 规范检查
- [ ] 实现弱引用管理（事件订阅）

**交付物**
- 内存分析报告
- 修复 PR 列表

**验收标准**
- 24 小时运行内存增长 < 50MB
- 无未释放的非托管资源

---

### 🔧 M5-004 · Native AOT 编译配置

| 字段 | 内容 |
|------|------|
| **所属模块** | `07-release` |
| **优先级** | 🔴 P0 |
| **依赖** | M5-003 |
| **预估工时** | 3d |

**核心任务清单**
- [ ] 启用 `<PublishAot>true</PublishAot>`
- [ ] 配置 `PublishTrimmed` 与 `TrimMode`
- [ ] 处理 AOT 不兼容 API（反射、动态加载）
- [ ] 使用源生成器替代反射（`[DynamicallyAccessedMembers]`）
- [ ] 配置 `rd.xml` 保留必要元数据
- [ ] 验证 ONNX Runtime AOT 兼容性
- [ ] 验证 Avalonia AOT 兼容性

**交付物**
- AOT 编译配置
- AOT 兼容性修复清单

**验收标准**
- `dotnet publish -c Release` 生成单文件可执行
- 启动时间 ≤ 500ms
- 无运行时 JIT

---

### 🔧 M5-005 · 反射消除与源生成

| 字段 | 内容 |
|------|------|
| **所属模块** | `07-release` |
| **优先级** | 🔴 P0 |
| **依赖** | M5-004 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 审计所有反射使用点
- [ ] 替换为源生成器（`System.Text.Json`、序列化）
- [ ] 实现配置类的 AOT 友好加载
- [ ] 实现插件系统的 AOT 适配
- [ ] 验证 `IL2026` / `IL3050` 警告清零

**交付物**
- AOT 兼容代码库
- 警告清零报告

**验收标准**
- `dotnet publish` 无 AOT 警告
- 所有功能在 AOT 下正常工作

---

### 🔧 M5-006 · 安装包与自动更新

| 字段 | 内容 |
|------|------|
| **所属模块** | `07-release` |
| **优先级** | 🟡 P1 |
| **依赖** | M5-005 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 实现 Windows 安装包（MSIX / Inno Setup）
- [ ] 实现 Linux 包（AppImage / deb）
- [ ] 实现自动更新检查（基于 GitHub Releases）
- [ ] 实现增量更新（差分下载）
- [ ] 实现版本回滚

**交付物**
- 安装包构建脚本
- 自动更新服务

**验收标准**
- 一键安装 / 卸载
- 更新过程不丢失用户数据

---

### 🔧 M5-007 · 文档与帮助

| 字段 | 内容 |
|------|------|
| **所属模块** | `08-docs` |
| **优先级** | 🟡 P1 |
| **依赖** | M5-001 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 编写用户手册（安装、使用、故障排查）
- [ ] 编写开发者文档（架构、扩展指南）
- [ ] 编写 API 参考（DocFX / mdBook）
- [ ] 录制教学视频
- [ ] 编写 FAQ

**交付物**
- 完整文档站点
- 视频教程

**验收标准**
- 新用户可独立完成安装使用
- 开发者可基于文档扩展功能

---

### 🔧 M5-008 · Beta 发布与反馈收集

| 字段 | 内容 |
|------|------|
| **所属模块** | `07-release` |
| **优先级** | 🟡 P1 |
| **依赖** | M5-006, M5-007 |
| **预估工时** | 2d |

**核心任务清单**
- [ ] 发布 Beta 版本（GitHub Releases）
- [ ] 实现反馈收集（Issue 模板、问卷）
- [ ] 实现遥测系统（可选、匿名）
- [ ] 收集崩溃报告（Sentry / 自建）
- [ ] 整理反馈 → 下一版本规划

**交付物**
- Beta 发布公告
- 反馈收集渠道

**验收标准**
- Beta 用户 ≥ 50 人
- 关键 Bug 修复率 ≥ 90%

---

### ✅ M5 验收标准（最终发布）

- [ ] Native AOT 单文件可执行
- [ ] 端到端延迟 ≤ 100ms（P95）
- [ ] 24 小时稳定运行
- [ ] 完整文档与安装包
- [ ] Beta 用户反馈积极

---

## 📋 GitHub Issue 模板（可直接复制使用）

### 📝 Issue 模板

```markdown
## [M{n}-{nnn}] {Issue 标题}

**所属模块**：`{module}`  
**优先级**：🔴 P0 / 🟡 P1 / 🟢 P2  
**预估工时**：{X}d  
**依赖**：#{issue_id}, #{issue_id}  
**Milestone**：M{n}  

### 📋 核心任务清单
- [ ] 任务 1
- [ ] 任务 2
- [ ] 任务 3

### 🎯 交付物
- 交付物 1
- 交付物 2

### ✅ 验收标准
- 标准 1
- 标准 2

### 🔧 技术要点
- 要点 1
- 要点 2

### 📚 参考资料
- 链接 1
- 链接 2
```

---

## 📊 Issue 总览表（可直接导入 GitHub）

| Issue ID | 标题 | 模块 | 优先级 | 依赖 | 工时 |
|----------|------|------|--------|------|------|
| M1-001 | 初始化解决方案与项目骨架 | bootstrap | 🔴 P0 | — | 1d |
| M1-002 | 配置 CI/CD 与开发环境 | bootstrap | 🔴 P0 | M1-001 | 1d |
| M1-003 | 日志与配置基础设施 | infrastructure | 🔴 P0 | M1-001 | 1d |
| M1-004 | Windows.Graphics.Capture 屏幕捕获服务 | capture | 🔴 P0 | M1-003 | 3d |
| M1-005 | 基于 Channels 的帧管道 | capture | 🔴 P0 | M1-004 | 2d |
| M1-006 | 捕获区域选择 UI | capture | 🟡 P1 | M1-004 | 1d |
| M1-007 | MVVM 基础设施 | infrastructure | 🔴 P0 | M1-001 | 1d |
| M1-008 | 帧预处理管线（OpenCvSharp4） | capture | 🟡 P1 | M1-005 | 2d |
| M2-001 | ONNX Runtime + DirectML 集成 | vision | 🔴 P0 | M1-005 | 2d |
| M2-002 | 模型加载与版本管理 | vision | 🔴 P0 | M2-001 | 2d |
| M2-003 | OpenCV 预处理管线（推理） | vision | 🔴 P0 | M2-001 | 2d |
| M2-004 | 目标检测模块（YOLO） | vision | 🔴 P0 | M2-003 | 3d |
| M2-005 | OCR / 文本识别模块 | vision | 🟡 P1 | M2-004 | 3d |
| M2-006 | 推理结果后处理与关联 | vision | 🟡 P1 | M2-004, M2-005 | 2d |
| M2-007 | 模型热插拔与降级策略 | vision | 🟡 P1 | M2-002 | 1d |
| M2-008 | 推理性能监控 | vision | 🟢 P2 | M2-001 | 1d |
| M3-001 | 游戏状态数据模型 | reasoning | 🔴 P0 | M2-006 | 2d |
| M3-002 | 卡牌 / 元素数据库 | reasoning | 🔴 P0 | M3-001 | 2d |
| M3-003 | 状态追踪器（历史窗口） | reasoning | 🔴 P0 | M3-001 | 3d |
| M3-004 | 概率引擎（核心算法） | reasoning | 🔴 P0 | M3-002, M3-003 | 4d |
| M3-005 | 决策建议引擎 | reasoning | 🟡 P1 | M3-004 | 3d |
| M3-006 | 游戏规则引擎 | reasoning | 🟡 P1 | M3-001 | 3d |
| M3-007 | 模拟框架（What-If） | reasoning | 🟢 P2 | M3-004 | 2d |
| M3-008 | 状态验证与错误恢复 | reasoning | 🟡 P1 | M3-003 | 2d |
| M4-001 | Avalonia UI 11 项目初始化 | ui | 🔴 P0 | M1-007 | 1d |
| M4-002 | 透明置顶悬浮窗（Windows） | ui | 🔴 P0 | M4-001 | 3d |
| M4-003 | 跨平台窗口抽象层 | ui | 🟡 P1 | M4-002 | 2d |
| M4-004 | 自定义渲染系统（GPU） | ui | 🟡 P1 | M4-002 | 3d |
| M4-005 | HUD 组件库 | ui | 🟡 P1 | M4-004 | 3d |
| M4-006 | 设置面板 | ui | 🟡 P1 | M4-005 | 2d |
| M4-007 | 主题与本地化 | ui | 🟢 P2 | M4-001 | 2d |
| M4-008 | 快捷键与系统托盘 | ui | 🟢 P2 | M4-006 | 1d |
| M5-001 | 端到端集成测试 | integration | 🔴 P0 | M1–M4 | 3d |
| M5-002 | 性能剖析与优化 | integration | 🔴 P0 | M5-001 | 3d |
| M5-003 | 内存泄漏检测与修复 | integration | 🔴 P0 | M5-002 | 2d |
| M5-004 | Native AOT 编译配置 | release | 🔴 P0 | M5-003 | 3d |
| M5-005 | 反射消除与源生成 | release | 🔴 P0 | M5-004 | 2d |
| M5-006 | 安装包与自动更新 | release | 🟡 P1 | M5-005 | 2d |
| M5-007 | 文档与帮助 | docs | 🟡 P1 | M5-001 | 2d |
| M5-008 | Beta 发布与反馈收集 | release | 🟡 P1 | M5-006, M5-007 | 2d |

---

## 🎯 关键里程碑节点（Milestone Gates）

| 节点 | 准入条件 | 准出条件 |
|------|----------|----------|
| **M1 Gate** | 解决方案可编译 | 捕获 → 帧队列 → 落盘全链路 Demo |
| **M2 Gate** | M1 全绿 | 帧 → 推理 → 检测结果可视化 |
| **M3 Gate** | M2 全绿 | 视觉 → 状态 → 概率 → 建议（命令行输出） |
| **M4 Gate** | M3 全绿 | 悬浮窗显示分析结果 |
| **M5 Gate** | M4 全绿 | AOT 单文件发布，Beta 用户可使用 |

---

## 📈 风险登记（Risk Register）

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| ONNX Runtime AOT 不兼容 | 🔴 高 | 中 | 提前验证，预留 CPU 后端 |
| Avalonia 透明窗口在某些游戏中被遮挡 | 🟡 中 | 中 | 实现多级置顶策略 |
| Windows.Graphics.Capture 被反作弊检测 | 🔴 高 | 高 | 仅用于单机 / 模拟器场景 |
| GPU 驱动兼容性问题 | 🟡 中 | 中 | 实现 CPU 降级 |
| 模型体积过大影响分发 | 🟡 中 | 低 | 模型按需下载 |

---

## ✅ 下一步行动

1. **创建 GitHub 仓库** `ChanSight/ChanSight`
2. **创建 5 个 Milestone**（M1–M5）
3. **批量导入 40 个 Issue**（使用上方模板）
4. **创建 Project Board**（看板视图：Backlog / In Progress / Review / Done）
5. **分配 M1-001 ~ M1-008** 给核心开发者
6. **每周 Milestone Review** 检查准入准出条件

---

> 📌 **备注**：本路线图为初始版本，每个 Milestone 结束后应根据实际进度、风险与反馈进行动态调整。建议使用 GitHub Milestones + Issues + Projects 三件套进行可视化追踪。

---

## Qwen 3.7 Flash ($(@{name=Qwen 3.7 Flash; id=qwen/qwen3.7-flash; outFile=docs/out_issues_qw.json}.id))

以下为【铲瞳 (ChanSight)】项目交付 Codex CLI 前，面向开发团队的**分阶段里程碑路线图**与**GitHub Issues 任务清单**。内容已针对 `.NET 8/9 Native AOT + Avalonia 11 + ONNX(DirectML) + WGC + OpenCvSharp4 + Channels` 技术栈进行深度适配，可直接用于 GitHub Projects / Issues 创建。

---
## 📅 整体里程碑规划（M1 → M5）

| 里程碑 | 名称 | 核心目标 | 周期预估 | 关键产出 |
|:---|:---|:---|:---|:---|
| **M1** | 基础骨架与捕获管道 | 项目初始化、AOT 兼容配置、Avalonia 壳层、WGC 帧捕获、Channel 骨架 | 1~2 周 | 可运行空壳、帧流采集、Producer-Consumer 通道 |
| **M2** | 视觉推理与模型管线 | ONNX Runtime DirectML 集成、OpenCv 预处理、张量转换、推理流水线打通 | 1~2 周 | 端到端推理链路、GPU 加速验证、零拷贝帧流转 |
| **M3** | 状态推演与概率算法 | 眼动点平滑滤波、坐标映射、ROI 状态机、内存池与热路径优化 | 1~2 周 | 低延迟状态输出、抗抖动算法、GC 压力可控 |
| **M4** | 跨平台 Avalonia 悬浮窗 | 拖拽透明窗体、设置面板、快捷键管理、实时性能 HUD | 1 周 | 完整交互 UI、配置持久化、性能监控覆盖 |
| **M5** | 闭环联调与 AOT 发布 | 全链路压测、内存泄漏排查、AOT 编译修复、安装包与文档 | 1~2 周 | 稳定 Release、AOT 合规包、用户手册、CI/CD |

---
## 📦 详细 GitHub Issues 拆解

### 🔹 M1: 基础骨架与捕获管道
#### Issue `M1-I1 feat(core): Initialize .NET 8/9 + Native AOT Project Structure`
- **模块**: `Core / Build`
- **核心任务**:
  - [ ] 创建解决方案与分层目录 (`src/ChanSight.Core`, `src/ChanSight.UI`, `src/ChanSight.Pipeline`)
  - [ ] 配置 `.csproj`：`<TargetFramework>net8.0</TargetFramework>`、`<PublishAot>true</PublishAot>`、`<TrimmerDefaultAction>link</TrimmerDefaultAction>`
  - [ ] 添加 AOT 兼容性标记：`<InvariantGlobalization>false</InvariantGlobalization>`、`<OptimizationPreference>Speed</OptimizationPreference>`
  - [ ] 配置 `json` 序列化上下文（`[JsonSerializable]`）与反射白名单（如需要）
- **交付物**: 可编译的 AOT 候选项目文件、目录结构规范
- **验收标准**: `dotnet publish -c Release -o out/aot` 无致命 AOT 错误；静态分析通过 `ILLink` 警告收敛至 <5 个可控项。

#### Issue `M1-I2 feat(ui): Create Avalonia 11 Shell & MVVM Base Infrastructure`
- **模块**: `UI / MVVM`
- **核心任务**:
  - [ ] 引入 `Avalonia.Desktop`、`CommunityToolkit.Mvvm`
  - [ ] 搭建 `App.axaml`、`MainWindow.axaml`（初始隐藏或最小化）
  - [ ] 实现 `BaseViewModel`、`RelayCommand`、`ObservableProperty` 基类
  - [ ] 配置 `Avalonia.Diagnostics` 仅用于 Dev 模式
- **交付物**: 空壳 UI 工程、MVVM 基础设施代码
- **验收标准**: 窗口可正常显示/隐藏；ViewModel 属性变更可触发 UI 更新；无同步阻塞调用。

#### Issue `M1-I3 feat(capture): Implement Windows.Graphics.Capture Frame Source`
- **模块**: `Capture / WGC`
- **核心任务**:
  - [ ] 引入 `Microsoft.Windows.SDK.Contracts` 与 `Microsoft.Graphics.Win2D`
  - [ ] 封装 `WgcCaptureService`：支持窗口/全屏捕获、分辨率自适应、帧率控制
  - [ ] 实现 `IDXGISurface` → `BitmapFrame` → `byte[]/IMemoryOwner` 转换
  - [ ] 添加捕获异常重试与设备丢失处理逻辑
- **交付物**: 捕获服务类、帧数据流接口 `IFrameSource`
- **验收标准**: 捕获帧率 ≥ 60 FPS；内存不泄漏；支持动态切换目标窗口。

#### Issue `M1-I4 feat(pipeline): Setup System.Threading.Channels Producer-Consumer Skeleton`
- **模块**: `Pipeline / Concurrency`
- **核心任务**:
  - [ ] 使用 `Channel.CreateBounded<T>(new BoundedChannelOptions(3) { SingleWriter = true })`
  - [ ] 实现 `FrameProducer`（WGC 驱动）与 `FrameConsumer`（预留推理槽位）
  - [ ] 接入 `CancellationToken` 优雅关闭机制
  - [ ] 添加背压处理（Backpressure）：队列满时丢弃最旧帧或阻塞生产者
- **交付物**: 通道管道代码、测试用例
- **验收标准**: 高负载下无死锁；丢帧策略可配置；`tryWrite`/`WaitToWriteAsync` 行为符合预期。

---

### 🔹 M2: 视觉推理与模型管线
#### Issue `M2-I1 feat(inference): Integrate ONNX Runtime DirectML Backend`
- **模块**: `Inference / ONNX`
- **核心任务**:
  - [ ] 引用 `Microsoft.ML.OnnxRuntime.DirectML`
  - [ ] 配置 `OrtSessionOptions`：`SetExecutionMode(Parallel)`、单线程 intra/inter op（降低延迟）
  - [ ] 实现 `OnnxSessionManager`：模型加载、会话复用、资源释放
  - [ ] 添加 DirectML 设备枚举与 fallback 机制（CPU 降级）
- **交付物**: 推理引擎包装类、模型加载器
- **验收标准**: 首次加载 ≤ 2s；后续推理延迟 ≤ 16ms（60Hz）；GPU 利用率 > 70%。

#### Issue `M2-I2 feat(cv): Configure OpenCvSharp4 Preprocessing Pipeline`
- **模块**: `CV / Preprocess`
- **核心任务**:
  - [ ] 引入 `OpenCvSharp4` 与原生运行时包
  - [ ] 实现 `IImagePreprocessor`：Resize、Normalize、BGR→RGB、Batch 拼接
  - [ ] 使用 `MatPool` 或 `ArrayPool<byte>` 减少 GC 分配
  - [ ] 确保 AOT 兼容：禁用动态类型解析，固定 P/Invoke 签名
- **交付物**: 预处理管线、张量格式转换器
- **验收标准**: 预处理耗时 ≤ 3ms；连续运行 1h 无内存增长；输出 Tensor 形状匹配模型输入。

#### Issue `M2-I3 feat(pipeline): Connect Channels to CV → ONNX → Postprocess Flow`
- **模块**: `Pipeline / Integration`
- **核心任务**:
  - [ ] 组装 `FrameConsumer`：读取 Channel → CV 预处理 → ONNX Run → 后处理
  - [ ] 实现异步流水线：`async ValueTask` 避免线程池饥饿
  - [ ] 添加推理结果缓存与时间戳对齐
  - [ ] 编写单元测试模拟帧流注入
- **交付物**: 完整推理消费端、集成测试
- **验收标准**: 端到端延迟 ≤ 30ms；吞吐量稳定；异常帧自动跳过不崩溃。

---

### 🔹 M3: 状态推演与概率算法
#### Issue `M3-I1 feat(algo): Implement Gaze Point Smoothing & Temporal Filtering`
- **模块**: `Algorithm / Smoothing`
- **核心任务**:
  - [ ] 实现 EMA/Kalman 滤波器组合
  - [ ] 添加眨眼检测与静默期过滤（避免无效跳变）
  - [ ] 暴露可调参数：`SmoothingFactor`、`MaxDeviation`、`CooldownMs`
  - [ ] 基准测试对比原始 vs 滤波后轨迹连续性
- **交付物**: 平滑算法类、参数配置模型
- **验收标准**: 轨迹抖动 ≤ 2px；响应延迟增加 ≤ 5ms；参数热更新生效。

#### Issue `M3-I2 feat(algo): Coordinate Mapping & ROI State Machine`
- **模块**: `Algorithm / Mapping`
- **核心任务**:
  - [ ] 实现屏幕坐标系 ↔ 摄像头视野映射（透视/仿射变换）
  - [ ] 设计 `GazeState` 枚举：`Idle`, `Focus`, `Scan`, `Blink`, `Error`
  - [ ] 构建有限状态机（FSM）处理区域进入/离开事件
  - [ ] 支持多 ROI 优先级仲裁
- **交付物**: 映射引擎、状态机实现、事件总线
- **验收标准**: 坐标误差 ≤ 1.5% 屏幕宽度；状态切换无振荡；日志可追溯。

#### Issue `M3-I3 perf(algo): Optimize Memory Allocation in Hot Path`
- **模块**: `Performance / Memory`
- **核心任务**:
  - [ ] 替换所有 `new byte[]`/`new Mat()` 为对象池
  - [ ] 使用 `Span<T>`/`Memory<T>` 替代切片拷贝
  - [ ] 启用 `DOTNet Garbage Collection Stats` 监控 Gen2 触发频率
  - [ ] AOT 预编译优化：移除未使用泛型实例
- **交付物**: 内存优化报告、对象池组件
- **验收标准**: 长稳运行 2h GC 次数下降 ≥ 80%；峰值内存 ≤ 150MB。

---

### 🔹 M4: 跨平台 Avalonia 悬浮窗与交互
#### Issue `M4-I1 feat(ui): Build Draggable Floating Window & Transparency`
- **模块**: `UI / Window`
- **核心任务**:
  - [ ] 创建 `FloatingOverlayWindow`：无边框、允许透明、置顶
  - [ ] 实现鼠标拖拽逻辑（`PointerPressed/Moved/Released`）
  - [ ] 绑定 DPI 缩放适配（`VisualRoot` 坐标转换）
  - [ ] 添加最小化到托盘/系统通知区
- **交付物**: 悬浮窗控件、拖拽服务
- **验收标准**: 拖拽无卡顿；透明度 0~100% 可调；多显示器坐标正确。

#### Issue `M4-I2 feat(ui): Settings Panel & Hotkey Management`
- **模块**: `UI / Settings`
- **核心任务**:
  - [ ] 设计设置页：捕获源、模型路径、平滑系数、快捷键绑定
  - [ ] 集成 `HotKeyManager`（基于 `GlobalKeyboardHook` 或 `RegisterHotKey` API）
  - [ ] 配置持久化：`JSON` 本地存储 + 版本迁移
  - [ ] 提供重置默认值与导出配置功能
- **交付物**: 设置界面、快捷键服务、配置管理器
- **验收标准**: 设置即时生效；重启后配置保留；全局快捷键不冲突。

#### Issue `M4-I3 feat(ui): Real-time Metrics Overlay (FPS, Latency, Confidence)`
- **模块**: `UI / Telemetry`
- **核心任务**:
  - [ ] 实现 HUD 叠加层：FPS、推理延迟、置信度、GPU 温度
  - [ ] 使用 `WriteableBitmap` 或 `DrawingContext` 高性能绘制
  - [ ] 支持 HUD 开关与位置拖动
  - [ ] 对接 `DiagnosticListener` 收集性能指标
- **交付物**: HUD 控件、指标收集器
- **验收标准**: 绘制开销 ≤ 1ms；数值刷新 ≥ 30Hz；不影响主渲染帧。

---

### 🔹 M5: 闭环联调、性能优化与 Native AOT 发布
#### Issue `M5-I1 test(integration): End-to-End Latency & Stability Testing`
- **模块**: `Testing / QA`
- **核心任务**:
  - [ ] 编写自动化集成测试：模拟帧流 → 推理 → 状态输出
  - [ ] 压力测试：72h 连续运行、窗口切换、分辨率突变
  - [ ] 延迟剖面分析：使用 `Stopwatch` + `Activity` 打点
  - [ ] 修复临界竞态条件与死锁场景
- **交付物**: 测试套件、稳定性报告
- **验收标准**: 平均延迟 ≤ 35ms；P99 ≤ 50ms；无崩溃/内存泄漏。

#### Issue `M5-I2 build(aot): Resolve AOT Compatibility & Trim Warnings`
- **模块**: `Build / AOT`
- **核心任务**:
  - [ ] 处理 `ILLink` 警告：添加 `[DynamicDependency]`、`[RequiresUnreferencedCode]`
  - [ ] 验证 OpenCvSharp4 与 ONNX Runtime 原生库 AOT 打包
  - [ ] 配置 `NativeLibraries` 嵌入或相对路径分发
  - [ ] 生成 `readytoRun` 优化镜像
- **交付物**: 纯净 AOT 编译产物、依赖清单
- **验收标准**: `dotnet publish` 成功且无运行时 `TypeInitializationException`；启动时间 ≤ 800ms。

#### Issue `M5-I3 release: Package, Installer & Documentation`
- **模块**: `Release / Docs`
- **核心任务**:
  - [ ] 制作 NSIS/MSIX 安装包（含 DirectML 运行时、OpenCV DLL）
  - [ ] 编写 `README.md`、架构图、API 说明、故障排查指南
  - [ ] 配置 GitHub Actions CI/CD：`build → test → publish → release`
  - [ ] 添加许可证与版权声明
- **交付物**: 安装包、文档仓库、CI 流水线
- **验收标准**: 一键安装无管理员权限要求；文档覆盖 90% 用户场景；CI 绿色通过。

---
## 🗂️ GitHub 任务追踪结构建议

### 1. 项目看板布局（GitHub Projects）
| 列名 | 含义 |
|:---|:---|
| `📥 Backlog` | 未排期需求/技术债 |
| `🔨 M1~M5 Active` | 当前里程碑进行中 |
| `🧪 In Review` | PR 待合并/Issue 待验收 |
| `✅ Done` | 已交付并验证 |
| `🐛 Blocked` | 依赖阻塞/外部问题 |

### 2. 标签体系（Labels）
```text
priority:critical / priority:high / priority:medium
type:feat / type:fix / type:perf / type:test / type:docs / type:build
milestone:m1 / milestone:m2 / ... / milestone:m5
area:core / area:ui / area:pipeline / area:inference / area:algo
status:aot-ready / status:needs-native-deps
```

### 3. Issue 模板建议（`.github/ISSUE_TEMPLATE/`）
```yaml
name: Feature / Fix / Perf
body:
  - type: dropdown
    id: module
    attributes: { label: 所属模块, options: [Core, UI, Capture, Pipeline, Inference, Algo, Build] }
  - type: textarea
    id: description
    attributes: { label: 核心任务清单, placeholder: "- [ ] ...\n- [ ] ..." }
  - type: input
    id: deliverables
    attributes: { label: 交付物 }
  - type: textarea
    id: acceptance
    attributes: { label: 验收标准, placeholder: "量化指标、边界条件、性能阈值" }
```

---
## ⚠️ 关键工程约束提醒（供 Codex CLI 提示词参考）
1. **Native AOT 限制**：禁止 `Activator.CreateInstance`、`Type.GetType(string)`、动态 LINQ；所有 JSON 序列化需显式声明 `JsonSerializableContext`。
2. **OpenCvSharp4 + AOT**：必须通过 `runtime.win-x64.OpenCvSharp4.runtime.native` 分发原生 DLL；避免在热路径调用 `Mat.ToString()` 等反射密集型方法。
3. **DirectML 线程模型**：`OrtSession` 非线程安全，需实例隔离或加锁；建议单消费者模式保延迟。
4. **Channel 背压策略**：生产环境推荐 `dropOldest` 或 `block`，严禁无限扩容导致 OOM。
5. **Avalonia 渲染**：悬浮窗需设置 `RenderTransform` 而非 `Canvas.Left/Top` 以提升合成性能；启用 `UseDirectX11_2` 硬件加速。

---
此路线图与 Issue 清单已结构化至可执行级别，可直接导入 GitHub Projects 或通过 CLI 批量创建。如需生成对应 `issue-create.sh` 脚本或 Codex Prompt 模板，可提供进一步指令。

---
