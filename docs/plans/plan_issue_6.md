# Issue #6 Implementation Plan: [CLI] 命令行交互录制工具与实机测试运行 (Spectre.Console 仪表盘)

## 1. 任务背景与目标
- **目标 Issue**: `Issue #6: [CLI] 命令行交互录制工具与实机测试运行`
- **所属阶段**: `Milestone 1: 屏幕捕获与对局录制采样工具 (M1 最终交付物)`
- **核心目的**: 基于 `Spectre.Console` 构建轻量、交互友好、实时显示帧率/写入状态的 CLI 控制台工具，将 `WindowFinder`、`WgcCaptureService`、`VideoRecorderService`、`DatasetSamplerService` 与全局 `F6` 启停热键完整串联，使用户能够在终端一键启动并录制实机对局数据。

---

## 2. 详细技术实现方案

### 2.1 CLI 架构与交互设计 (`src/ChanSight.Cli/`)
1. **启动与依赖注入 (`Program.cs`)**:
   - 使用 `Host.CreateDefaultBuilder()` 注册所有核心服务：
     - `AddChanSightCore()`
     - `AddChanSightCapture()`
     - `AddChanSightRecorder()`
   - 注入 `ConsoleApp` / `InteractiveDashboard` 主调度器。
2. **交互式控制台菜单与命令**:
   - **`WindowSelectCommand`**: 自动扫描并展示当前活跃的模拟器/游戏窗口列表（标题、PID、分辨率），支持方向键选择或自动绑定最优匹配项。
   - **`InteractiveDashboard` (Spectre.Console Live Panel)**:
     - 实时展示录制会话状态（`IDLE` / `RECORDING` / `PAUSED` / `STOPPED`）
     - 实时显示当前帧率 (FPS)、已录制时长、写入帧数、采样图片数
     - 提示全局热键指南：`F6` 开始/停止录制，`F7` 手动快照，`Q` 退出
3. **安全退出与优雅注销**:
   - 监听 `Console.CancelKeyPress` (Ctrl+C)，安全停止捕获与录制，确保视频文件与元数据 `meta.json` 完整封包。

### 2.2 单元与集成测试 (`tests/ChanSight.Tests/Cli/`)
- 测试 CLI 服务容器装配与命令参数解析逻辑。

---

## 3. 验收标准
- `dotnet build` 0 错误 0 警告；
- 启动 CLI 工具可清晰呈现控制台仪表盘，支持自动检测游戏窗口并响应 `F6` 开始录制；
- 录制结束时在 `datasets/recordings/` 目录下产出完整有效的 MP4 视频、帧序列图片及 `meta.json`。
