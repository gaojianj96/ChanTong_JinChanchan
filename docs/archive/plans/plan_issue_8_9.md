# Issue #8 & Issue #9 Implementation Plan: [Vision] 1920x1080 标准坐标系 ROI 自适应映射器与 4x7 棋盘/9格备战席切片流水线

## 1. 任务背景与目标
- **目标 Issue**:
  - `Issue #8: [Vision] 1920x1080 标准坐标系下的 ROI 自适应映射器 (RoiMapper)`
  - `Issue #9: [Vision] 棋盘 4x7 六角网格与备战席 9 格子区域自动切片流水线 (GridSlicer)`
- **所属阶段**: `Milestone 2: 数据集标注、预处理与 4x7 棋盘/商店 ROI 定位 (M2 最终交付物)`
- **核心目的**:
  1. 构建 1920x1080 基准分辨率下的金铲铲各核心功能区域（商店5卡槽、金币、阶段、等级、血量、对手侧边栏、4x7棋盘、9格备战席）的标准 ROI 定义体系；
  2. 实现 `RoiMapper` 自适应坐标转换器：无论游戏/模拟器处于任意物理分辨率（如 2560x1440、1600x900、1280x720 或带黑边比例），均能双向准确映射至 1920x1080 规范化坐标系；
  3. 实现 `GridSlicer`：精确对 4x7 (28个六边形格点) 棋盘网格与 9 格备战席进行透视矫正、子网格划分与局部图像切片，输出结构化的切片张量供后续 YOLO 与 OCR 模型消费。

---

## 2. 详细技术实现方案

### 2.1 ROI 区域规范定义 (`src/ChanSight.Vision/Models/`)
- `RoiRegionType`: `BoardArea`, `PlayerBench`, `ShopCards`, `Gold`, `Level`, `Hp`, `StageRound`, `OpponentsSidebar`, `ActiveTraits`
- `NormalizedRoi`: 归一化比例坐标 `[XRatio, YRatio, WidthRatio, HeightRatio]`
- `RoiDefinitionTable`: 包含 1920x1080 官方基准像素坐标与归一化比例参数。

### 2.2 核心服务实现 (`src/ChanSight.Vision/Services/`)
1. **`RoiMapperService` (`IRoiMapperService`)**:
   - `MapToTarget(Rect canonicalRect, int sourceWidth, int sourceHeight)`: 将 1920x1080 基准坐标映射至当前实际分辨率的物理像素 Rect；
   - `CropRoi(Mat frame, RoiRegionType regionType)`: 根据当前实际帧分辨率，精准裁切对应功能区域的 `Mat`；
   - `CropShopSlots(Mat frame)`: 一次性切出 5 个商店英雄卡牌槽位的子图像列表。
2. **`GridSlicerService` (`IGridSlicerService`)**:
   - `SliceBenchSlots(Mat frame)`: 精确分割 9 格备战席槽位图像 (`BenchSlot[0..8]`)；
   - `SliceBoardHexagons(Mat frame)`: 根据 4 行 x 7 列的棋盘交错几何坐标系，提取 28 个六边形棋盘格点的中心点坐标与局部裁剪框 (`BoardHexSlot[row, col]`)；
   - `CropCell(Mat frame, int row, int col)`: 提取指定行列的弈子局部图像。

### 2.3 单元测试覆盖 (`tests/ChanSight.Tests/Vision/`)
- `RoiMapperServiceTests.cs`:
  - 验证在 1920x1080、2560x1440、1280x720 等不同分辨率下的坐标缩放映射准确性；
  - 验证各区域 ROI 裁切尺寸与边界保护。
- `GridSlicerServiceTests.cs`:
  - 验证 9 格备战席与 28 格棋盘六角格切片索引、坐标生成与裁切输出。

---

## 3. 验收标准
- `dotnet build` 0 错误 0 警告；
- 单元测试覆盖全部分辨率映射算法与棋盘/备战席切片；
- 在真实录制的金铲铲截图上，能够准确裁切出 5 格商店、9 格备战席与 4x7 棋盘网格。
