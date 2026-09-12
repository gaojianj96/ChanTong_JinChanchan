# Issue #4 Implementation Plan: [Recorder] 异步高吞吐对局录制管道

## 1. 任务背景与目标
- **目标 Issue**: `Issue #4: [Recorder] 异步高吞吐对局录制管道 (Frame Buffer & Video Writer)`
- **所属阶段**: `Milestone 1: 屏幕捕获与对局录制采样工具`
- **核心目的**: 基于 `System.Threading.Channels` 搭建异步无锁视频录制管道，使用 OpenCV `VideoWriter` 实现高吞吐 MP4/AVI 视频编码，支持全局快捷键 `F6` 启停录制，并在对局结束后自动输出 `meta.json` 对局元数据。

---

## 2. 详细技术实现设计

### 2.1 核心服务与组件 (`src/ChanSight.Recorder/`)
1. **`VideoRecorderOptions`**:
   - `OutputDirectory`: 默认输出目录（如 `datasets/recordings/`）。
   - `CodecFourCC`: 视频编码器 FourCC（如 `mp4v`、`avc1` 或 `XVID`）。
   - `TargetFps`: 目标录制帧率（如 30 FPS）。
   - `EnableHotKey`: 是否启用 `F6` 全局热键监听。
2. **`VideoRecorderService` (`IVideoRecorder`)**:
   - `StartRecordingAsync(IFrameSource frameSource, WindowTarget target, VideoRecorderOptions? options)`:
     - 创建会话目录 `datasets/recordings/{session_id}/`。
     - 初始化 OpenCV `VideoWriter` 写入 `match_video.mp4`。
     - 启动后台消费线程从 `IFrameSource`（如 `WgcCaptureService`）读取 `CapturedFrame` 进行写盘。
   - `PauseRecordingAsync()`, `ResumeRecordingAsync()`, `StopRecordingAsync()`:
     - 控制录制状态流转，处理时长统计。
     - 停止时释放 `VideoWriter` 并序列化输出 `meta.json`（录制起止时间、有效帧数、总时长、分辨率、丢帧数）。
3. **`GlobalHotKeyService` (`IGlobalHotKeyService`)**:
   - 封装 Win32 `RegisterHotKey` / `UnregisterHotKey` API。
   - 监听 `F6`（0x75）按键消息，触发 `OnHotKeyTriggered` 事件，实现录制状态的一键切换（开始/停止）。

### 2.2 单元测试设计 (`tests/ChanSight.Tests/Recorder/`)
- `VideoRecorderServiceTests.cs`:
  - 接入 `MockFrameSource` 测试录制完整生命周期（Start $\rightarrow$ Pause $\rightarrow$ Resume $\rightarrow$ Stop）。
  - 验证 `meta.json` 内容结构与正确性。
  - 验证异常状态与资源释放。

---

## 3. 验收标准
- `dotnet build` 0 错误 0 警告；
- 单元测试覆盖录制管道状态流转、帧写入与元数据生成；
- 能够稳定输出可播放的视频文件与 `meta.json`。
