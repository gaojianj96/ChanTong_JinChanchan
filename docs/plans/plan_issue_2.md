# Issue #2 Implementation Plan: [Capture] 金铲铲/模拟器游戏窗口自动发现与 DPI 适配绑定

## 1. 任务背景与目标
- **目标 Issue**: `Issue #2: [Capture] 金铲铲/模拟器游戏窗口自动发现与 DPI 适配绑定 (WindowFinder)`
- **所属阶段**: `Milestone 1: 屏幕捕获与对局录制采样工具`
- **核心目的**: 实现 Windows 平台下高精度、低侵入的游戏窗口查找器与状态监听器，支持自动识别主流安卓模拟器（MuMu、雷电、夜神等）及游戏客户端，精确获取排除系统阴影的物理客户区坐标与 Per-Monitor DPI 缩放比率。

---

## 2. 详细技术实现设计

### 2.1 Win32 P/Invoke 接口封装 (`src/ChanSight.Capture/Win32/`)
- `User32.cs`:
  - `EnumWindows`, `IsWindowVisible`, `GetWindowText`, `GetWindowTextLength`
  - `GetWindowThreadProcessId`, `GetClassName`, `GetClientRect`, `ClientToScreen`
  - `GetDpiForWindow` (Win10 1607+) 与 `DPI_AWARENESS_CONTEXT` 支持
- `DwmApi.cs`:
  - `DwmGetWindowAttribute(HWND, DWMWA_EXTENDED_FRAME_BOUNDS)` 获取消除 Windows 10/11 窗口透明外发光/阴影后的精确画面外框。

### 2.2 核心服务组件
1. **`WindowFinder` (`IWindowFinder`)**:
   - `FindTargetWindow(WindowSearchOptions? options)`: 枚举顶层窗口，根据标题关键词（默认：金铲铲之战、MuMu、雷电、夜神、Tencent）或指定 PID/HWND 进行匹配。
   - `GetWindowTarget(IntPtr hwnd)`: 构建完整的 `WindowTarget` 实体（含标题、句柄、真实物理 Rect、DPI 缩放因子）。
2. **`WindowStateMonitor` (`IWindowStateMonitor`)**:
   - 定期或基于 WinEventHook 监控目标窗口状态变化（移动、调整大小、最小化、关闭）。
   - 触发 `WindowBoundsChanged` 与 `WindowClosed` 事件。

### 2.3 单元测试覆盖
- 在 `tests/ChanSight.Tests/Capture/WindowFinderTests.cs` 中实现：
  - 模拟窗口列表过滤与关键词匹配测试
  - 坐标与 DPI 换算逻辑验证
  - 窗口无效/已关闭时的边界异常处理

---

## 3. 验收标准
- `dotnet build` 0 错误 0 警告；
- 单元测试覆盖窗口发现与 DPI 坐标解析算法；
- 能够准确识别金铲铲或模拟器窗口并输出精确物理分辨率。
