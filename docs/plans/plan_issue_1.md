# Issue #1 Implementation Plan: [Core] 搭建 C# (.NET 8/9) 多工程解决方案与基础框架

## 1. 任务背景与目标
- **目标 Issue**: `Issue #1: [Core] 搭建 C# (.NET 8/9) 多工程解决方案与基础框架 (ChanSight.sln)`
- **所属阶段**: `Milestone 1: 屏幕捕获与对局录制采样工具`
- **核心目的**: 建立低耦合、高内聚的多工程分层骨架，为后续 WGC 屏幕截取、异步录制与数据集采样奠定工程与依赖底座。

---

## 2. 工程目录结构设计 (Project Scaffolding)

```
ChanSight/
├── ChanSight.sln                         # 根解决方案
├── Directory.Build.props                 # 全局属性配置 (Nullable, C# 12, 统一版本)
├── src/
│   ├── ChanSight.Core/                   # 核心领域实体、通用接口与公共常量
│   │   ├── Models/                       # FrameData, SessionMeta, WindowInfo
│   │   ├── Interfaces/                   # ICaptureService, IVideoRecorder, IDatasetSampler
│   │   └── ChanSight.Core.csproj
│   ├── ChanSight.Capture/                # 屏幕捕获驱动 (WGC / DXGI 适配器)
│   │   ├── Win32/                        # P/Invoke 声明 (CsWin32 / DwmApi / User32)
│   │   ├── Services/                     # WindowFinder, WgcCaptureService
│   │   └── ChanSight.Capture.csproj
│   ├── ChanSight.Recorder/               # 对局录制与数据集采样服务
│   │   ├── Services/                     # VideoRecorderService, DatasetSamplerService
│   │   ├── Pipelines/                    # FrameQueue (System.Threading.Channels)
│   │   └── ChanSight.Recorder.csproj
│   └── ChanSight.Cli/                    # 命令行交互与仪表盘
│       ├── Commands/                     # RecordCommand, ListWindowsCommand
│       ├── Program.cs                    # 启动入口与 DI 容器配置
│       └── ChanSight.Cli.csproj
├── tests/
│   └── ChanSight.Tests/                  # 单元测试与集成测试
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

### 3.2 各工程 NuGet 依赖清单
1. **`ChanSight.Core`**:
   - `OpenCvSharp4` (4.10.0+)
   - `System.Threading.Channels` (8.0.0+)
2. **`ChanSight.Capture`**:
   - `ChanSight.Core` (ProjectRef)
   - `OpenCvSharp4.runtime.win`
   - `Microsoft.Windows.SDK.Contracts` / `CsWin32` (用于 WGC 与 D3D11 互操作)
3. **`ChanSight.Recorder`**:
   - `ChanSight.Core` (ProjectRef)
   - `ChanSight.Capture` (ProjectRef)
4. **`ChanSight.Cli`**:
   - `ChanSight.Recorder` (ProjectRef)
   - `Spectre.Console` (0.49.0+) (终端仪表盘)
   - `Microsoft.Extensions.Hosting` (8.0.0+) (DI与生命周期)

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
   - 定义 `IWindowFinder`、`ICaptureService`、`IVideoRecorder` 接口骨架
   - 定义 `CapturedFrame`（包含 `Mat`、时间戳、分辨率、帧序号）
4. **Step 4: 编译验证与基础单元测试**
   - 运行 `dotnet build` 验证 0 错误 0 警告
   - 编写 `ChanSight.Tests` 中的基础验证用例并运行 `dotnet test`

---

## 5. 潜在技术难点与风险控制

| 潜在风险 / 难点 | 影响评估 | 应对策略 |
| :--- | :--- | :--- |
| **OpenCvSharp 原生 Runtime 加载失败** | 无法正常处理 Mat 内存 | 显式引入 `OpenCvSharp4.runtime.win` 并在测试中验证 `Cv2.GetVersionString()` |
| **WinRT 依赖与 .NET 8 兼容性** | 捕获 API 无法实例化 | 确保采用标准 `CsWin32` 或最新 `Microsoft.Windows.SDK.NET.Ref` 绑定 |
| **多项目依赖循环** | 编译阻断 | 严格遵循 `Core -> Capture -> Recorder -> Cli` 单向依赖链路 |
