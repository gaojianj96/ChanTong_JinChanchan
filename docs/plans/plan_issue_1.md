# Issue #1 Implementation Plan: [Core] 搭建 C# (.NET 8/9) 多工程解决方案与基础框架

## 1. 任务背景与目标
- **目标 Issue**: `Issue #1: [Core] 搭建 C# (.NET 8/9) 多工程解决方案与基础框架 (ChanSight.sln)`
- **所属阶段**: `Milestone 1: 屏幕捕获与对局录制采样工具`
- **核心目的**: 建立低耦合、面向接口、支持依赖注入的多工程分层骨架，为后续 WGC 屏幕截取、异步无锁录制管道与数据集采样提供坚实底座。

---

## 2. 工程目录结构设计 (Project Scaffolding)

```
ChanSight/
├── ChanSight.sln                         # 根解决方案
├── Directory.Build.props                 # 全局属性配置 (Nullable, C# 12/13, 性能优化)
├── src/
│   ├── ChanSight.Core/                   # 纯抽象与领域实体 (零重依赖)
│   │   ├── Models/                       # CapturedFrame, WindowTarget, SessionMeta
│   │   ├── Interfaces/                   # IFrameSource, IScreenCaptureService, IVideoRecorder, IDatasetSampler
│   │   ├── Memory/                       # IFramePool (帧缓冲区复用接口)
│   │   └── ChanSight.Core.csproj
│   ├── ChanSight.Capture/                # 屏幕捕获驱动 (WGC / DXGI 实现)
│   │   ├── Win32/                        # P/Invoke 声明 (DwmApi, User32, CsWin32)
│   │   ├── Services/                     # WindowFinder, WgcCaptureService
│   │   └── ChanSight.Capture.csproj
│   ├── ChanSight.Recorder/               # 异步视频录制与数据集采样服务
│   │   ├── Services/                     # VideoRecorderService, DatasetSamplerService
│   │   ├── Pipelines/                    # BoundedFrameChannel (System.Threading.Channels)
│   │   └── ChanSight.Recorder.csproj
│   └── ChanSight.Cli/                    # 命令行交互、DI 容器与实时仪表盘
│       ├── Commands/                     # RecordCommand, ListWindowsCommand
│       ├── Dashboard/                    # Spectre.Console 状态仪表盘
│       ├── Program.cs                    # 启动入口与 HostBuilder DI 配置
│       └── ChanSight.Cli.csproj
├── tests/
│   └── ChanSight.Tests/                  # 单元测试与 MockFrameSource 模拟测试
│       ├── Mocks/                        # MockFrameSource
│       └── ChanSight.Tests.csproj
└── docs/                                 # 架构与开发规范文档
```

---

## 3. 依赖配置与 Directory.Build.props

### 3.1 全局构建属性 (`Directory.Build.props`)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <Authors>ChanSight Team</Authors>
    <Version>0.1.0</Version>
  </PropertyGroup>
</Project>
```

### 3.2 各工程 NuGet 依赖与职责划分
1. **`ChanSight.Core`**:
   - `Microsoft.Extensions.Logging.Abstractions` (8.0.0+)
   - `System.Threading.Channels` (8.0.0+)
   - `OpenCvSharp4` (4.10.0+) (仅定义 Mat 基础元数据与图像模型)
2. **`ChanSight.Capture`**:
   - `ChanSight.Core` (ProjectRef)
   - `OpenCvSharp4.runtime.win`
   - `Microsoft.Windows.SDK.NET.Ref` / `CsWin32` (用于 WGC/DXGI/WinRT 底层直采)
3. **`ChanSight.Recorder`**:
   - `ChanSight.Core` (ProjectRef)
   - `ChanSight.Capture` (ProjectRef)
4. **`ChanSight.Cli`**:
   - `ChanSight.Recorder` (ProjectRef)
   - `Microsoft.Extensions.Hosting` (8.0.0+) (DI 容器与生命周期)
   - `Microsoft.Extensions.Logging.Console` (8.0.0+)
   - `Spectre.Console` (0.49.0+) (交互式仪表盘)
5. **`ChanSight.Tests`**:
   - `Microsoft.NET.Test.Sdk`
   - `xunit` / `xunit.runner.visualstudio`
   - `FluentAssertions`

---

## 4. 实施步骤与交付流程 (Step-by-Step Implementation)

1. **Step 1: 创建解决方案与项目模板**
   - 运行 `dotnet new sln -n ChanSight`
   - 创建 `src/ChanSight.Core` 等 4 个核心工程与 `tests/ChanSight.Tests`
   - 将所有项目添加到 `ChanSight.sln`
2. **Step 2: 配置 Directory.Build.props 与 NuGet 依赖**
   - 编写根目录 `Directory.Build.props`
   - 分别为各项目添加对应的 NuGet PackageReference
3. **Step 3: 编写核心基础接口与领域实体**
   - 定义 `IFrameSource`、`IWindowFinder`、`IScreenCaptureService`、`IVideoRecorder` 接口骨架
   - 定义 `CapturedFrame`（包含 `Mat`、时间戳、分辨率、帧序号、`IDisposable` 资源管理）
   - 编写依赖注入扩展方法 `AddChanSightCore()`
4. **Step 4: 编译验证与基础单元测试**
   - 运行 `dotnet build` 验证 0 错误 0 警告
   - 在 `ChanSight.Tests` 中编写 `MockFrameSource` 验证用例并运行 `dotnet test`

---

## 5. 潜在技术难点与风险控制

| 潜在风险 / 难点 | 影响评估 | 应对策略 |
| :--- | :--- | :--- |
| **OpenCvSharp 原生 Runtime 加载失败** | 无法正常处理 Mat 内存 | 显式引入 `OpenCvSharp4.runtime.win` 并在测试中验证 `Cv2.GetVersionString()` |
| **高频大分辨率帧内存暴涨** | 触发频繁 GC 导致掉帧 | `CapturedFrame` 实现 `IDisposable`，结合 `Channels` 采用 `DropOldest` 背压丢帧策略 |
| **多项目依赖循环** | 编译阻断 | 严格遵循 `Core -> Capture -> Recorder -> Cli` 单向依赖链路，所有服务面向接口注入 |
