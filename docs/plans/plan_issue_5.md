# Issue #5 Implementation Plan: [Dataset] 对局数据集采样器 (按秒自动采样 + 独立快照热键)

## 1. 任务背景与目标
- **目标 Issue**: `Issue #5: [Dataset] 对局数据集采样器 (按秒自动采样 + 自定义快照热键)`
- **所属阶段**: `Milestone 1: 屏幕捕获与对局录制采样工具`
- **核心目的**: 在录制游戏画面的同时，通过定时采样器（如每 2 秒 / 5 秒）或独立快照热键（如 `F7` / `F8`，与 `F6` 启停完全解耦）抽取高清单帧图片（PNG/JPG），自动生成标准化标注目录结构 `datasets/recordings/{session_id}/frames/{timestamp}_{index}.jpg`，为后续视觉识别与 PaddleOCR 商店识别构建真实样本数据集。

---

## 2. 详细技术实现方案

### 2.1 核心服务组件 (`src/ChanSight.Recorder/`)
1. **`DatasetSamplerOptions`**:
   - `OutputDirectory`: 数据集输出根目录（默认 `datasets/`）。
   - `IntervalSeconds`: 自动采样周期（如 2.0 秒，支持浮点数）。
   - `ImageFormat`: 图片编码格式（`Jpg` 质量 95% 或 `Png` 无损）。
   - `SnapshotHotKey`: 单帧快照热键（默认 `F7`，虚拟键码 0x76）。
2. **`DatasetSamplerService` (`IDatasetSampler`)**:
   - `StartSamplingAsync(IFrameSource frameSource, WindowTarget target, DatasetSamplerOptions? options)`:
     - 监听帧流，基于 Stopwatch 精准计算间隔时间触发自动保存。
     - 监听独立快照事件，触发手动单帧写盘。
   - `TakeManualSnapshotAsync(CapturedFrame frame)`:
     - 立即将当前帧保存到 `snapshots/` 子目录，并记录快照元数据。
   - `StopSamplingAsync()`:
     - 停止采样，输出采样索引清单 `dataset_index.json`（图片路径、时间戳、帧序号、分辨率）。

### 2.2 单元测试覆盖 (`tests/ChanSight.Tests/Recorder/`)
- `DatasetSamplerServiceTests.cs`:
  - 接入 `MockFrameSource` 测试定时自动采样与手动快照逻辑。
  - 验证图片文件生成、目录结构命名规范与 `dataset_index.json` 元数据完整性。

---

## 3. 验收标准
- `dotnet build` 0 错误 0 警告；
- 单元测试覆盖自动采样与单帧快照；
- 能够按预设时间间隔和热键稳定输出清晰图片文件。
