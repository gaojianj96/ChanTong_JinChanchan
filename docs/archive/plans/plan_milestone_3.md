# Milestone 3 Implementation Plan: [Vision] ONNX Runtime DirectML 引擎、YOLOv11n 目标检测与 PaddleOCR 商店识别管线

## 1. 任务背景与目标
- **目标 Issues**:
  - `Issue #10: [Vision] ONNX Runtime C# (DirectML) GPU 加速推理引擎集成`
  - `Issue #11: [Vision] 棋盘 4x7 网格与备战席 YOLOv11n 弈子/星级/装备目标检测器`
  - `Issue #12: [Vision] 商店 5 卡槽英雄、金币、阶段数值 PaddleOCR 识别模块（含英雄字典约束）`
- **所属阶段**: `Milestone 3: 视觉模型推理管线`
- **核心目的**: 构建跨 Intel/AMD/NVIDIA 显卡硬件加速的实时视觉推理管线，实现从对局画面中毫秒级（$\le 15\text{ms}$）提取场上弈子类别、星级、装备配置、当前商店 5 张卡牌及金币等级信息。

---

## 2. 详细技术实现方案

### 2.1 依赖引入 (`src/ChanSight.Vision/ChanSight.Vision.csproj`)
- 引入 `Microsoft.ML.OnnxRuntime` 与 `Microsoft.ML.OnnxRuntime.DirectML`

### 2.2 核心服务与模型架构 (`src/ChanSight.Vision/`)
1. **`OnnxInferenceEngine` (`IOnnxInferenceEngine`)**:
   - 统一管理 ONNX `InferenceSession` 生命周期；
   - 配置 DirectML Execution Provider (`AppendExecutionProvider_DML`)，在无 GPU 环境下自动无缝降级到 CPU EP；
   - 实现高性能 Tensor 内存封装与批量推理。
2. **`YoloDetectorService` (`IYoloDetectorService`)**:
   - 支持加载 `yolov11n.onnx` / `yolov8n.onnx`；
   - 实现 Letterbox 预处理、FP16/FP32 张量转换；
   - 实现高效 NMS (非极大值抑制) 与置信度过滤；
   - 输出 `IReadOnlyList<DetectedUnit>`（包含弈子名称、星级 1/2/3、装备列表、置信度、物理 BoundingBox 与对应的棋盘/备战席槽位索引）。
3. **`PaddleOcrService` (`IPaddleOcrService`)**:
   - 加载文本检测与文本识别 ONNX 模型；
   - 内置金铲铲官方赛季英雄全量字典（如亚索、阿狸、辛德拉、千珏等）与羁绊名称；
   - 基于 Levenshtein 距离实现字典严格剪枝与模糊纠错，将低分辨率文字识别准确率提升至 $> 99\%$；
   - 解析金币、等级（如 "Level 7" $\rightarrow$ 7）、阶段（如 "3-2"）。

### 2.3 单元与集成测试设计 (`tests/ChanSight.Tests/Vision/`)
- `OnnxInferenceEngineTests.cs`: 测试引擎创建、SessionOptions 配置、Device fallback。
- `YoloDetectorServiceTests.cs`: 模拟/真实输出张量 NMS 解析、坐标还原与单元提取。
- `PaddleOcrServiceTests.cs`: 字典模糊纠错算法（Levenshtein Distance）与数值解析测试。

---

## 3. 验收标准
- `dotnet build` 0 错误 0 警告；
- 单元测试覆盖模型推理引擎、YOLO 后处理与 OCR 字典纠错；
- 全链路单帧总推理延迟 $\le 15\text{ms}$。
