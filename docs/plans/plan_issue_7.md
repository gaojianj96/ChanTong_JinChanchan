# Issue #7 Implementation Plan: [Vision] 数据集清洗、感知哈希去重与标注格式转换

## 1. 任务背景与目标
- **目标 Issue**: `Issue #7: [Vision] 对局采样帧数据清洗、图像去重（感知哈希）与标注格式转换（YOLO 格式）`
- **所属阶段**: `Milestone 2: 数据集标注、预处理与 4x7 棋盘/商店 ROI 定位`
- **核心目的**: 对实机采集的对局图片帧进行自动化清洗与去重（使用差分感知哈希 dHash / 平均哈希 aHash / 汉明距离算法），过滤高相似度重复静止帧与全黑/全白模糊坏帧；并提供将有效帧导出为标准 YOLO / COCO 格式标注数据集目录的功能。

---

## 2. 详细技术实现方案

### 2.1 建立 `ChanSight.Vision` 工程 (`src/ChanSight.Vision/`)
- TargetFramework: `net8.0-windows10.0.19041.0` (兼容跨平台基础库，引用 `ChanSight.Core`)
- 引用依赖: `OpenCvSharp4`, `OpenCvSharp4.runtime.win`, `System.Threading.Channels`
- 添加到 `ChanSight.sln`

### 2.2 核心算法与服务设计
1. **`PerceptualHashService` (`IPerceptualHashService`)**:
   - `ComputeDHash(Mat image)`: 缩放至 9x8 灰度图，计算相邻像素差分，生成 64-bit uint64 哈希值。
   - `ComputeAHash(Mat image)`: 缩放至 8x8 灰度图，计算均值比较，生成 64-bit uint64 哈希值。
   - `CalculateHammingDistance(ulong hash1, ulong hash2)`: 使用内置 SIMD/BitOperations.PopCount 极速计算汉明距离（相似度阈值默认 HammingDistance $\le 5$ 判定为重复帧）。
2. **`DatasetCleanerService` (`IDatasetCleanerService`)**:
   - `CleanDatasetAsync(string sessionDirectory, DatasetCleanOptions? options)`:
     - 扫描 `datasets/recordings/{session_id}/` 下的原始帧与快照
     - 过滤低对比度/模糊/黑屏坏帧
     - 按时间序对连续帧进行 dHash 去重，仅保留显著变化的对局动作帧
     - 输出去重后的精简数据集 `datasets/cleaned/{session_id}/` 与清洗报告 `clean_report.json`
3. **`YoloDatasetExporter` (`IYoloDatasetExporter`)**:
   - 将清洗后的帧结构化导出为标准 YOLO 标注结构：
     ```
     datasets/yolo_export/
     ├── images/
     │   ├── train/
     │   └── val/
     ├── labels/
     │   ├── train/
     │   └── val/
     └── data.yaml
     ```

### 2.3 单元测试设计 (`tests/ChanSight.Tests/Vision/`)
- `PerceptualHashTests.cs`: 测试同一图片的 dHash 距离为 0，微小扰动距离很小，不同图片距离很大。
- `DatasetCleanerServiceTests.cs`: 模拟生成连续重复帧与差异帧，验证去重率与清洗输出。

---

## 3. 验收标准
- `dotnet build` 0 错误 0 警告；
- 单元测试覆盖感知哈希算法、相似度比对与数据集清洗导出；
- 能对实测产生的帧数据集快速完成去重过滤，输出清晰的 `clean_report.json`。
