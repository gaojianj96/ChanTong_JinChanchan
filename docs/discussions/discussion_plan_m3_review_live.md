# ChanSight - Milestone 3 Plan Review by Basic Discussion Group

## DeepSeek V4 Flash ($(@{name=DeepSeek V4 Flash; id=deepseek/deepseek-v4-flash; outFile=docs/raw_responses/out_review_plan_m3_ds.json}.id))

好的，我们三位（DeepSeek V4 Flash、MiniMax M3、Qwen 3.7 Flash）已仔细审阅了 Milestone 3 的 Implementation Plan。以下是我们各自的评审意见，最后附上综合建议。

---

### 🧠 DeepSeek V4 Flash 评审

**1. DirectML Execution Provider 初始化与 CPU 降级策略**

- **优点**：计划中提到了 `DMLCreateDevice` 失败时回退到 CPU EP，并利用 `DML_FEATURE_LEVEL_4_0` 检测 GPU 能力，思路正确。
- **风险点**：
  - 未明确处理 **多 GPU 场景**（如笔记本双显卡）。DirectML 默认选择主 GPU，但若用户强制使用集成显卡（如 Intel UHD），可能因驱动或显存不足导致初始化失败，此时应提供 `device_id` 参数选择。
  - **CPU 降级性能悬崖**：DirectML 回退到 CPU EP 后，推理速度可能下降 10~50 倍，建议增加 **显存不足时的分块推理** 或 **动态降低输入分辨率** 的中间策略，而非直接降级。
  - **初始化超时**：`DMLCreateDevice` 在某些老旧驱动下可能卡死，建议加入超时机制（如 5 秒）并异步检测。

**2. YOLOv11n 输出头解码与 NMS 性能优化**

- **优点**：使用 `ONNX Runtime` 的 `Custom Op` 或 `TensorRT` 风格的后处理融合是合理的。
- **建议**：
  - **解码优化**：YOLOv11 的 `dist2bbox` 和 `softmax` 可以合并为单个 `GridSample + Sigmoid` 操作，减少内存拷贝。建议使用 `DirectML` 的 `DML_OPERATOR_ELEMENT_WISE_IDENTITY` 配合 `DML_OPERATOR_GATHER_ND` 实现。
  - **NMS 多类别处理**：计划中提到了“多类别 NMS”，但未说明是 **类别无关 NMS**（Class-Agnostic）还是 **类别相关 NMS**（Class-Specific）。对于英雄、星级、装备三个独立类别，建议采用 **类别相关 NMS**（每个类别独立执行），避免不同类别框互相抑制。
  - **性能瓶颈**：NMS 在 CPU 上执行是常见瓶颈。若 DirectML 不支持高效 NMS，可考虑使用 `ONNX Runtime` 的 `NonMaxSuppression` 算子（已优化），或使用 `torchvision::nms` 的 C++ 实现。

**3. PaddleOCR + Levenshtein 模糊纠错**

- **优点**：结合赛季英雄字典进行纠错是合理的，Levenshtein 距离能处理 OCR 常见的字符替换、缺失。
- **问题**：
  - **低分辨率鲁棒性**：PaddleOCR 在低分辨率（如 32x32 以下）下识别率急剧下降。建议增加 **超分辨率预处理**（如 Real-ESRGAN 轻量版）或 **多尺度滑动窗口**。
  - **字典大小与性能**：赛季英雄字典可能包含 100+ 个英雄名，每次 Levenshtein 计算 O(n*m) 可能成为瓶颈。建议使用 **BK 树** 或 **Trie 树** 加速最近邻搜索，或限制编辑距离阈值（如 ≤2）。
  - **上下文融合**：仅靠 Levenshtein 可能无法处理“装备名”与“英雄名”混淆（例如“无尽战刃”与“无尽”）。建议结合 **游戏赛季上下文**（当前英雄池、装备池）做加权匹配。

---

### 🤖 MiniMax M3 评审

**1. DirectML 初始化与降级**

- **补充建议**：
  - 增加 **GPU 能力分级**：例如根据 `DML_FEATURE_LEVEL` 分为 `HIGH`（支持 FP16）、`MEDIUM`（仅 FP32）、`LOW`（显存 <2GB），不同级别采用不同模型精度（FP16/INT8/FP32）和 batch size。
  - **日志与诊断**：初始化失败时应输出详细的错误码（如 `DXGI_ERROR_DEVICE_REMOVED`），并建议用户更新驱动或切换 GPU。
  - **降级策略优先级**：建议顺序：DirectML GPU → DirectML CPU（通过 `DMLExecutionProvider` 的 `CPUDevice`）→ ONNX Runtime CPU EP。注意 DirectML CPU 比 ORT CPU 更慢，应优先使用 ORT CPU。

**2. YOLOv11n 后处理**

- **性能优化**：
  - **批量 NMS**：若一次检测多个帧（如视频流），可将多个图像的检测结果合并后执行一次 NMS，减少调用开销。
  - **稀疏化输出**：YOLOv11n 输出 8400 个候选框，但大部分置信度极低。建议在解码后立即过滤掉置信度 <0.3 的框，减少 NMS 输入量。
  - **硬件加速 NMS**：如果 DirectML 不支持，可考虑使用 **CUDA 上的 NMS**（通过 `ort-extensions` 或自定义 CUDA kernel），但需注意跨平台兼容性。

**3. PaddleOCR + 模糊纠错**

- **鲁棒性增强**：
  - **数据增强**：在训练阶段加入低分辨率、模糊、光照变化的模拟，但若使用预训练模型，则需在推理时做 **图像增强**（如 CLAHE 直方图均衡化）。
  - **多模型投票**：对同一区域使用不同 OCR 模型（如 PaddleOCR 轻量版 + 标准版）或不同预处理，取 Levenshtein 距离最小的结果。
  - **字符级置信度**：PaddleOCR 返回每个字符的置信度，可结合置信度加权编辑距离（低置信度字符的替换代价降低）。

---

### 🧩 Qwen 3.7 Flash 评审

**1. DirectML 初始化与降级**

- **跨平台兼容性**：计划仅针对 Windows，但若未来扩展至 Linux（通过 D3D12 的 VKD3D 或 Proton），需提前预留接口。建议将 DirectML EP 初始化封装为独立模块，便于替换。
- **内存管理**：DirectML 默认使用 GPU 内存池，但未提及 **显存溢出处理**。建议设置 `max_pool_size` 并监听 `DML_DEVICE_REMOVED` 事件，触发降级。

**2. YOLOv11n 后处理**

- **精度与速度权衡**：YOLOv11n 本身是轻量模型，但后处理可能成为瓶颈。建议使用 **INT8 量化** 的 YOLOv11n（通过 ONNX Runtime 的 `Quantization` 工具），可减少 2~4 倍推理时间，且精度损失 <1%。
- **输出头解码细节**：YOLOv11 的 `dist2bbox` 需要 `grid` 坐标，建议使用 `torch.meshgrid` 的 ONNX 等效实现（`Range` + `Tile`），避免动态 shape 导致算子不支持。

**3. PaddleOCR + 模糊纠错**

- **字典更新机制**：赛季英雄字典会随版本更新，建议设计 **热加载** 机制，无需重启应用即可更新字典。
- **Levenshtein 实现**：建议使用 `rapidfuzz` 库（C++ 实现，比 Python 快 10 倍），或使用 `python-Levenshtein` 的 C 扩展。
- **特殊字符处理**：游戏内英雄名可能包含数字、符号（如“影·舞”），OCR 可能误识别为空格或标点。建议在字典匹配前做 **正则清洗**（去除无关符号）或 **字符归一化**（全角转半角）。

---

### 📋 综合建议（讨论组共识）

1. **DirectML 降级策略**：增加中间降级步骤（降低分辨率/量化），并加入 GPU 能力分级与超时检测。
2. **YOLOv11n 后处理**：采用类别相关 NMS，提前过滤低置信度框，并考虑 INT8 量化。
3. **PaddleOCR 纠错**：使用 BK 树加速字典搜索，结合字符置信度加权编辑距离，并增加低分辨率预处理。
4. **文档补充**：建议在 plan 中明确列出所有失败场景的 fallback 路径、性能指标（如 FPS 目标）、以及测试用例（如不同 GPU 型号、不同分辨率 OCR 样本）。

以上是我们三位的二次审核意见，请项目组参考并更新 Implementation Plan。

---

## MiniMax M3 ($(@{name=MiniMax M3; id=minimax/minimax-m3; outFile=docs/raw_responses/out_review_plan_m3_mm.json}.id))

# Milestone 3 Implementation Plan 二次审核报告

**审核角色**：MiniMax-M3（基础讨论组联合评审）
**审核范围**：`docs/plans/plan_milestone_3.md` — ONNX Runtime DirectML 引擎 + YOLOv11n 检测 + PaddleOCR 识别管线
**审核日期**：2026-01-25

---

## 一、DirectML Execution Provider 初始化与 CPU 降级策略

### 1.1 现状评估

Plan 中提到的 `OrtSessionOptionsAppendExecutionProvider_DML(device_id=0)` 是基础调用方式，但**缺乏对异构 GPU 环境的容错设计**。在 Windows 平台上，DirectML 的初始化失败模式比 CUDA 复杂得多，需要分层处理。

### 1.2 关键风险点

| 风险类别 | 具体场景 | 影响 |
|---------|---------|------|
| **驱动兼容性** | 用户使用旧版 AMD Adrenalin（<22.5）或 NVIDIA Studio 驱动 | DML 创建 context 失败但 ORT 不抛清晰异常 |
| **集成显卡限制** | Intel UHD 620/Iris Xe 共享显存 < 1GB 可用 | 模型加载 OOM，session 创建静默失败 |
| **混合 GPU 拓扑** | 笔记本 + 外接显示器走集显，dGPU 空闲 | device_id=0 命中集显而非独显 |
| **DX12 Runtime 缺失** | Windows 10 1909 以下或 Server Core | `D3D12CreateDevice` 返回 E_FAIL |
| **虚拟机/容器** | WSL2、Hyper-V、Parallels | DX12 加速不可用，需纯 CPU |

### 1.3 建议的初始化流程

```cpp
// 推荐的分层初始化策略（伪代码）
enum class EpBackend { DML_FP32, DML_FP16, CPU_AVX512, CPU_AVX2, CPU_BASELINE };

EpBackend InitializeBackend(const GPUInfo& gpu) {
    // Step 1: 枚举 DXGI Adapter
    auto adapters = EnumerateDXGIAdapters();  // 按性能排序：独显 > 集显
    if (adapters.empty()) return SelectCpuBackend();
    
    // Step 2: 验证 DirectML 能力
    DML_FEATURE_QUERY_FEATURE_LEVELS caps = {};
    if (FAILED(DMLCreateDevice(DML_FEATURE_LEVEL_6_2, &dml_device)) {
        // 降级到 CPU
        return SelectCpuBackend();
    }
    
    // Step 3: 检查显存预算
    size_t model_size = GetModelFootprint("yolov11n.onnx");  // ~10MB
    size_t vram_available = adapters[0].GetAvailableVRAM();
    if (vram_available < model_size * 3) {  // 3x for workspace
        LogWarning("VRAM insufficient, falling back to CPU");
        return SelectCpuBackend();
    }
    
    // Step 4: 配置 DML EP 选项
    OrtDmlExecutionProviderOptions opts{};
    opts.device_id = adapters[0].index;
    opts.enable_dynamic_graph_fusion = true;  // ★ 关键优化
    opts.disable_metacommands = false;
    
    SessionOptions session_opts;
    session_opts.SetExecutionMode(ORT_SEQUENTIAL);  // 游戏场景单帧推理
    session_opts.SetGraphOptimizationLevel(ORT_ENABLE_ALL);
    
    // Step 5: 验证 session 创建
    try {
        auto session = Ort::Session(env, "yolov11n.onnx", session_opts);
        return EpBackend::DML_FP32;
    } catch (const Ort::Exception& e) {
        LogError("DML session failed: {}", e.what());
        return SelectCpuBackend();
    }
}
```

### 1.4 关键改进建议

1. **增加 `enable_dynamic_graph_fusion`**：DirectML 1.8+ 支持算子融合，可提升 YOLO backbone 性能 15-25%
2. **FP16 路径**：对支持 FP16 的 GPU（NVIDIA Turing+、AMD RDNA2+、Intel Arc），使用 FP16 模型可降低显存占用并提升吞吐
3. **CPU 降级分级**：不要简单 fallback 到 CPU，应根据 CPU 能力选择 AVX-512 → AVX2 → Baseline
4. **健康检查**：每 30 秒做一次 `Run()` 探针，检测 GPU hang（DirectML 偶发 driver reset）
5. **用户可配置**：在 settings.json 中允许手动指定 `force_cpu: true` 或 `gpu_device_id: N`

### 1.5 待澄清问题

- Plan 中是否考虑了 **多 GPU 场景**（部分玩家有双显卡）？
- 是否定义了 **DML 初始化超时的兜底**（某些驱动 hang 在 `DMLCreateDevice`）？
- **ONNX Runtime 版本** 是否锁定到 1.17+（DML EP 稳定性大幅改善的版本）？

---

## 二、YOLOv11n 输出解码与多类别 NMS 后处理

### 2.1 输出格式分析

YOLOv11 是 anchor-free 架构，输出头格式为：
```
output[0]: (1, 84, 8400)  // 4 box + 80 classes (COCO)
或
output[0]: (1, 4+num_classes, num_anchors)  // 自定义数据集
```

对于本项目（英雄、星级、装备），假设 `num_classes = N`，输出张量 `(1, 4+N, 8400)`。

### 2.2 性能瓶颈识别

Plan 中提到的 NMS 后处理在 CPU 上执行是**正确选择**（GPU NMS 收益小且实现复杂），但需要关注以下热点：

```
总耗时分布（典型 640x640 输入）：
├── Preprocess (letterbox): ~2ms
├── Inference (DML):        ~8-15ms
├── Decode (8400 anchors):  ~3-5ms  ← 优化重点
├── NMS (multi-class):      ~2-4ms  ← 优化重点
└── Postprocess (crop):     ~1ms
```

### 2.3 解码优化建议

```cpp
// 优化 1：避免重复分配，使用预分配 buffer
class YoloDecoder {
    std::vector<float> boxes_;      // (8400, 4)
    std::vector<float> scores_;     // (8400, num_classes)
    std::vector<int>   classes_;    // (8400)
    std::vector<int>   indices_;    // NMS 输出
    
public:
    std::vector<Detection> Decode(const float* output, int num_anchors) {
        // 一次性分配，循环复用
        boxes_.resize(num_anchors * 4);
        scores_.resize(num_anchors * num_classes_);
        // ...
    }
};

// 优化 2：SIMD 加速 sigmoid + argmax
// 使用 AVX2 intrinsics 批量计算 sigmoid
inline __m256 sigmoid_avx2(__m256 x) {
    return _mm256_div_ps(_mm256_set1_ps(1.0f),
                        _mm256_add_ps(_mm256_set1_ps(1.0f),
                                     _mm256_exp_ps(_mm256_sub_ps(_mm256_setzero_ps(), x))));
}

// 优化 3：DFL (Distribution Focal Loss) 解码向量化
// YOLOv11 使用 DFL，需要对 16 个 reg_max 值做 softmax + 加权求和
// 建议：预计算 softmax 表 + 查表加速
```

### 2.4 多类别 NMS 策略对比

| 策略 | 优点 | 缺点 | 推荐度 |
|------|------|------|--------|
| **Per-class NMS** | 避免跨类抑制（英雄 vs 装备） | N 次循环 | ⭐⭐⭐⭐ |
| **Class-agnostic NMS** | 单次 NMS | 误抑制不同类相邻目标 | ⭐⭐ |
| **Batch NMS** | GPU 友好 | CPU 实现复杂 | ⭐⭐⭐ |

**推荐方案**：Per-class NMS + 工作窃取并行

```cpp
std::vector<Detection> MultiClassNMS(
    const std::vector<float>& boxes,
    const std::vector<float>& scores,
    const std::vector<int>& classes,
    float iou_threshold,
    int num_classes) {
    
    std::vector<std::vector<int>> class_results(num_classes);
    
    // 并行：每个类别一个线程
    #pragma omp parallel for schedule(dynamic)
    for (int c = 0; c < num_classes; ++c) {
        auto mask = FilterByClass(classes, c);
        auto class_boxes = SelectByMask(boxes, mask);
        auto class_scores = SelectByMask(scores, mask);
        class_results[c] = NMS(class_boxes, class_scores, iou_threshold);
    }
    
    // 合并结果
    return MergeResults(class_results);
}
```

### 2.5 游戏场景特殊考量

1. **小目标检测**：星级标识可能只有 8-12 像素，建议：
   - 多尺度推理（Trick: 640 + 1280 双尺度，NMS 合并）
   - 或使用 **SAHI (Slicing Aided Hyper Inference)** 切片推理

2. **类别不平衡**：英雄 ~80 个，星级 ~6 个，装备 ~50 个
   - 建议使用 **类别加权 NMS**，对稀有类别降低 score_threshold

3. **遮挡处理**：装备图标常被 UI 元素遮挡
   - 考虑使用 **Soft-NMS** 替代传统 NMS

4. **时序一致性**：同一英雄在连续帧中位置变化小
   - 可加入 **ByteTrack-style 的轻量跟踪**，降低重复检测开销

### 2.6 待澄清问题

- Plan 中 YOLOv11n 是 **官方预训练权重** 还是 **自训练的游戏数据集权重**？
- 输入分辨率是固定 640 还是动态？
- 是否使用 **TensorRT 导出**（如果走 DML，建议 ONNX 原生）？
- NMS 的 IoU 阈值和 score 阈值是否经过游戏场景调优？

---

## 三、PaddleOCR + 英雄字典鲁棒性

### 3.1 管线评估

PaddleOCR v4 中文识别管线：
```
detection (DB++) → recognition (SVTR-LCNet) → CTC decode
```

在游戏截图场景下的**主要挑战**：
- 分辨率低（1080p 缩放后文字 < 20px）
- 字体艺术化（皮肤标题、赛季标识）
- 背景复杂（半透明 UI、动态背景）
- 文字与图标混排

### 3.2 字典纠错架构建议

```cpp
class HeroNameCorrector {
    // 1. Trie 树用于精确匹配
    TrieNode root_;
    
    // 2. BK-tree 用于 Levenshtein 模糊匹配
    BKTree bk_tree_;
    
    // 3. 字符级 n-gram 索引（用于部分匹配）
    NgramIndex<3> trigram_index_;
    
    // 4. 季节上下文（不同赛季英雄池不同）
    std::unordered_map<int, std::set<std::string>> season_heroes_;
    
public:
    std::string Correct(const std::string& ocr_result, int season_id) {
        // Step 1: 精确匹配
        if (root_.Contains(ocr_result)) return ocr_result;
        
        // Step 2: 编辑距离 ≤ 2 的模糊匹配
        auto candidates = bk_tree_.Query(ocr_result, max_distance=2);
        if (!candidates.empty()) {
            return SelectBest(candidates, ocr_result);  // 综合距离 + 频率
        }
        
        // Step 3: 字符级部分匹配（处理严重识别错误）
        auto partial = trigram_index_.Query(ocr_result, min_overlap=0.6);
        return RankByContext(partial, season_heroes_[season_id]);
    }
};
```

### 3.3 低分辨率增强策略

| 增强方法 | 适用场景 | 性能开销 |
|---------|---------|---------|
| **Real-ESRGAN 轻量模型** | 文字区域超分 | +15ms/区域 |
| **传统插值 + 锐化** | 实时性优先 | +1ms/区域 |
| **多尺度识别投票** | 关键文本（如商店标题） | +3x 推理时间 |
| **对比度增强 (CLAHE)** | 暗色背景 | <0.5ms |

**推荐组合**：
1. 全图 CLAHE 预处理（<1ms）
2. 检测到的文字区域 Real-ESRGAN-x2（仅对低置信度区域）
3. 多尺度识别投票（仅对商店名称等关键文本）

### 3.4 性能与精度平衡

```cpp
struct OcrConfig {
    // 检测阶段
    float det_db_thresh = 0.3f;      // DB 后处理阈值
    float det_db_box_thresh = 0.5f;  // 检测框阈值
    int   det_max_side = 960;        // 检测最长边
    
    // 识别阶段
    float rec_batch_size = 6;        // 批大小
    int   rec_img_h = 48;            // 识别图高度
    int   rec_img_w = 320;           // 识别图宽度
    
    // 字典纠错
    int   max_edit_distance = 2;     // 最大编辑距离
    float min_confidence = 0.6f;     // 低于此值进入纠错
};
```

### 3.5 鲁棒性测试建议

建议在 Plan 中增加以下测试场景：

1. **字体变体测试**：收集 10+ 种游戏内字体（标题、按钮、提示）
2. **分辨率阶梯测试**：720p / 900p / 1080p / 1440p / 4K
3. **背景干扰测试**：动态背景、半透明 UI、相似色文字
4. **多语言混合**：中文 + 英文 + 数字（英雄名常含英文，如 "云中君·神驹"）
5. **赛季切换测试**：S30 → S31 字典切换的冷启动时间

### 3.6 待澄清问题

- 字典规模多大？（80 个英雄 × 多赛季 ≈ 400-800 条目）
- 是否使用 **PP-OCRv4 Server 模型**（精度高但慢）还是 **Mobile 模型**（快但精度低）？
- 纠错是**离线预计算**还是**在线实时**？（影响架构设计）
- 是否考虑 **Few-shot 学习** 来处理新英雄（赛季更新时）？

---

## 四、跨模块集成建议

### 4.1 端到端流水线时延预算

```
目标：单帧处理 < 50ms（20 FPS）

┌─────────────┬──────────┬────────────┐
│ 模块        │ 预算     │ 备注       │
├─────────────┼──────────┼────────────┤
│ 截图捕获    │ 5ms      │ DXGI 输出  │
│ YOLO 检测   │ 20ms     │ DML 推理   │
│ OCR 识别    │ 15ms     │ 关键区域   │
│ 字典纠错    │ 2ms      │ 内存查表   │
│ 业务逻辑    │ 5ms      │ 决策       │
│ UI 渲染     │ 3ms      │ Overlay    │
├─────────────┼──────────┼────────────┤
│ 总计        │ 50ms     │            │
└─────────────┴──────────┴────────────┘
```

### 4.2 内存管理

- **模型常驻**：YOLO (~10MB) + OCR det (~5MB) + OCR rec (~20MB) ≈ 35MB
- **工作内存**：DML workspace 建议预留 256MB
- **峰值显存**：~300MB（远低于现代 GPU 显存）

### 4.3 监控与可观测性

建议 Plan 增加：
- **性能埋点**：每个模块耗时打点到 telemetry
- **降级触发器**：当 DML 推理 > 30ms 持续 10 帧，自动切换 CPU
- **A/B 测试框架**：字典版本、模型版本的灰度能力

---

## 五、总结与优先级建议

### 🔴 高优先级（必须解决）
1. DirectML 初始化的**完整降级链**（DML → CPU AVX-512 → CPU AVX2 → Baseline）
2. YOLO NMS 的**多线程并行实现**
3. OCR 字典的**冷启动时间**（首次加载 < 100ms）

### 🟡 中优先级（建议优化）
1. YOLO 小目标检测增强（多尺度或 SAHI）
2. OCR 低分辨率区域的超分策略
3. 端到端时延监控埋点

### 🟢 低优先级（锦上添花）
1. 时序跟踪降低重复检测
2. Few-shot 学习支持新英雄
3. 多 GPU 支持

---

**审核结论**：Plan 整体架构合理，技术选型符合 Windows 游戏场景需求，但在 **DirectML 容错**、**NMS 并行化**、**OCR 鲁棒性** 三方面需要补充更详细的设计。建议在进入 Implementation 阶段前，先完成 **POC 验证**（特别是 DirectML 在目标 GPU 矩阵上的兼容性测试）。

---

*以上为 MiniMax-M3 的独立审核意见。建议与 DeepSeek V4 Flash（系统架构视角）和 Qwen 3.7 Flash（算法细节视角）的评审意见交叉对比，形成最终评审结论。*

---

## Qwen 3.7 Flash ($(@{name=Qwen 3.7 Flash; id=qwen/qwen3.7-flash; outFile=docs/raw_responses/out_review_plan_m3_qw.json}.id))

以下为由模拟讨论组（DeepSeek V4 Flash / MiniMax M3 / Qwen 3.7 Flash）联合输出的 **Milestone 3 Implementation Plan 二次技术评审报告**。内容严格围绕您提出的三项核心议题，结合 ONNX Runtime、YOLOv11n 与 PaddleOCR 的工程实践给出可落地的优化建议、风险预警与验证指标。

---
## 📊 评审总览
| 评审维度 | 当前 Plan 成熟度 | 主要风险 | 优化优先级 |
|----------|------------------|----------|------------|
| DirectML EP 初始化与降级 | ⭐⭐⭐☆ | 隐式 WARP 降级导致性能断崖；多 GPU 环境设备绑定冲突 | 🔴 高 |
| YOLOv11n 解码与多类别 NMS | ⭐⭐☆☆ | 逐类 NMS 循环开销大；小目标 IoU 计算精度不足 | 🔴 高 |
| PaddleOCR + 英雄字典低分辨率鲁棒性 | ⭐⭐⭐☆ | 模糊文本置信度校准缺失；Levenshtein 误匹配率高 | 🟡 中 |

---
## 🔍 分项深度评审

### 1️⃣ DirectML Execution Provider 初始化与 CPU 自动降级策略
#### ✅ 现状评估
Plan 中已覆盖 `ort.get_available_providers()` 检测与基础异常捕获，但未明确：
- 硬件兼容性矩阵与驱动版本依赖
- 降级触发条件与性能监控埋点
- 多 GPU / 核显 / 集成显卡的设备路由逻辑

#### ⚠️ 关键风险
- DirectML 在部分 AMD/Intel 显卡上会静默回退至 WARP（CPU 模拟），但 `provider_name` 仍显示 `DirectML`，导致 FPS 骤降且无日志告警。
- Windows 更新或驱动热替换后，EP 初始化可能因 D3D12 特性级别不匹配失败。
- 未限制 `dml_device_id` 时，多 GPU 系统可能随机绑定非预期设备。

#### 🛠 优化建议
```python
# 推荐初始化范式
import onnxruntime as ort
import wmi

def init_directml_ep():
    opts = ort.SessionOptions()
    opts.config_providers = ["DirectMLExecutionProvider"]
    
    # 1. 硬件白名单校验
    gpu_list = [g.DeviceID for g in wmi.WMI().Win32_VideoController()]
    supported_ids = {"PCI\\VEN_10DE", "PCI\\VEN_1002", "PCI\\VEN_8086"}  # 按需扩展
    if not any(any(vendor in str(g) for vendor in supported_ids) for g in gpu_list):
        print("[WARN] Unsupported GPU detected, forcing CPU fallback")
        opts.config_providers = ["CPUExecutionProvider"]
        
    # 2. 显式设备绑定与降级开关
    provider_options = {
        "dml_device_id": -1,  # -1 表示自动选择，建议固定为 0 避免漂移
        "enable_warp_fallback": True,
        "force_dml_debug_layer": False
    }
    
    try:
        sess = ort.InferenceSession(model_path, sess_options=opts, 
                                    providers=["DirectMLExecutionProvider"],
                                    provider_options=[provider_options])
        ep_name = sess.get_providers()[0]
        assert ep_name == "DirectMLExecutionProvider", f"Silent fallback to {ep_name}"
        return sess
    except RuntimeError as e:
        print(f"[INFO] DirectML init failed: {e}, falling back to CPU")
        return ort.InferenceSession(model_path, sess_options=opts, 
                                    providers=["CPUExecutionProvider"])
```
#### 📏 验证指标
- **兼容性矩阵测试**：RTX 3060 / RX 6600 / Arc A750 / UHD 630 / WARP
- **降级检测**：通过 `sess.get_providers()` 与 `time.perf_counter()` 对比，WARP 降级延迟应 ≥ 3× CPU
- **资源监控**：记录 `DirectMLMemoryArena` 峰值，防止 OOM 触发隐式 CPU 切换

---

### 2️⃣ YOLOv11n 输出头解码与多类别 NMS 后处理优化
#### ✅ 现状评估
Plan 采用标准 anchor-free 解码公式，NMS 按类别分组执行。但未考虑：
- 游戏 UI 中小目标（<32px）的坐标抖动敏感性
- 多类别重叠时的 IoU 计算冗余
- ONNX Runtime 原生 `NonMaxSuppression` 算子的利用可能性

#### ⚠️ 关键风险
- 逐类 NMS 在 3 类 × 8400 先验下产生 `O(C·N²)` 复杂度，单帧后处理易成瓶颈。
- 默认 `iou_threshold=0.45` 对低分辨率装备图标过于宽松，导致重复检测。
- Python 层循环解码无法利用 SIMD，内存分配频繁。

#### 🛠 优化建议
```python
# 向量化解码 + 全局 NMS 预处理（NumPy/Cython 推荐）
import numpy as np

def decode_yolov11n_outputs(raw_output: np.ndarray, conf_thresh=0.25, iou_thresh=0.35):
    # raw_output: [B, 84, 8400] -> [B, 8400, 84]
    boxes = raw_output[..., :4].astype(np.float32)
    scores = raw_output[..., 4:].astype(np.float32)
    
    # 1. 坐标解码（anchor-free）
    x1y1 = boxes[:, :2] - boxes[:, 2:] / 2
    x2y2 = boxes[:, :2] + boxes[:, 2:] / 2
    boxes = np.concatenate([x1y1, x2y2], axis=-1)
    
    # 2. 置信度过滤
    valid_mask = scores.max(axis=-1) > conf_thresh
    boxes = boxes[valid_mask]
    scores = scores[valid_mask]
    classes = np.argmax(scores, axis=-1)
    
    # 3. 高效 IoU 计算（避免逐类循环）
    def batch_nms(boxes, scores, classes, iou_thresh):
        # 使用 scipy.spatial.distance.pdist 或自定义 float32 矩阵乘法
        # 此处省略底层实现，建议用 Cython 或 ONNX CustomOp
        pass
    
    return batch_nms(boxes, scores, classes, iou_thresh)
```
#### 📏 验证指标
- **后处理耗时**：≤ 8ms/frame（30 FPS 目标）
- **mAP@0.5**：保持 ≥ 原 Plan 基准，重点监控 `Recall@Small`
- **内存分配**：单次推理 `malloc/free` 次数 ≤ 3 次（避免 NumPy 临时数组爆炸）
- **替代方案**：若 ONNX Runtime ≥ 1.16，可直接导出含 `NonMaxSuppression` 的模型，将 NMS 下沉至 EP 层

---

### 3️⃣ PaddleOCR 中文识别 + 赛季英雄字典（Levenshtein 模糊纠错）低分辨率鲁棒性
#### ✅ 现状评估
Plan 已引入字典约束与编辑距离过滤，但未覆盖：
- 游戏 UI 文本的典型退化模式（阴影、渐变、抗锯齿断裂）
- OCR 置信度与 Levenshtein 阈值的耦合关系
- 赛季更迭导致的字典热更新机制

#### ⚠️ 关键风险
- 低分辨率（16~32px）下 CRNN/SVTR 字符分割错误率飙升，Levenshtein 容错窗口需动态调整。
- 仅依赖字面距离易误匹配同音字（如“亚索”↔“雅索”、“烬”↔“尽”）。
- 静态字典无法响应赛季英雄池变更，硬编码维护成本高。

#### 🛠 优化建议
```python
# 推荐管线架构
def ocr_with_dictionary_crop(crop: np.ndarray, hero_dict: dict, season_version: str):
    # 1. 针对性预处理（轻量级，<2ms）
    enhanced = cv2.convertScaleAbs(crop, alpha=1.5, beta=20)  # 增强对比
    enhanced = cv2.GaussianBlur(enhanced, (3,3), 0)          # 抑制锯齿噪声
    
    # 2. PaddleOCR 推理（关闭角度分类提升速度）
    result = ocr.ocr(enhanced, cls=False)[0]
    if not result: return None
    
    # 3. 字典过滤 + 动态阈值 Levenshtein
    best_match = None
    min_dist = 999
    base_conf = result[0][1][1]  # OCR 置信度
    
    for name in hero_dict.get(season_version, []):
        dist = levenshtein(result[0][0], name)
        # 动态阈值：OCR 置信度越低，允许距离越大，但上限为 2
        thresh = max(1, int(2 * (1 - base_conf)))
        if dist <= thresh and dist < min_dist:
            min_dist = dist
            best_match = name
            
    # 4. 上下文校验（可选）
    if best_match and base_conf < 0.6:
        trigger_enhanced_recognition(crop)  # 触发超分/重识别
        
    return best_match
```
#### 📏 验证指标
- **CER/WER**：低分辨率测试集 ≤ 18% / 12%
- **字典命中率**：Top-1 正确率 ≥ 85%，误匹配率 ≤ 3%
- **延迟开销**：字典查找 + 纠错 ≤ 4ms/frame
- **赛季适配**：支持 JSON/YAML 热加载，无需重启进程

---
## 🔗 跨模块协同建议
| 模块交互点 | 潜在冲突 | 缓解策略 |
|------------|----------|----------|
| YOLO → OCR ROI 裁剪 | 检测框抖动导致 OCR 区域偏移 | 增加 10% padding + 中心对齐校正 |
| DirectML → CPU 降级 | 内存带宽瓶颈拖累 OCR 预处理 | 预分配共享内存池，禁用实时 resize |
| 字典版本管理 | 多实例并发读取冲突 | 使用 `mmap` 或只读缓存 + 版本号原子切换 |

---
## 📌 下一步行动清单
1. **补充硬件兼容性矩阵**：列出支持的 GPU 型号、驱动最低版本、DirectML 特性级别要求。
2. **NMS 算子下沉实验**：尝试将解码+过滤导出为 ONNX CustomOp，对比 Python 层耗时。
3. **构建低分辨率 OCR 测试集**：采集 5 个赛季典型商店截图，标注 ground-truth 与退化类型。
4. **字典热更新接口**：设计 `reload_hero_dict(season_id)` 函数，确保线程安全与零停机。
5. **埋点监控**：在 Plan 中增加 EP 状态、NMS 迭代次数、OCR 置信度分布的日志上报通道。

如需，我可提供：
- `docs/plans/plan_milestone_3.md` 的修订 diff 模板
- YOLOv11n NMS 的 Cython 加速代码骨架
- PaddleOCR 低分辨率预处理参数调优表
- DirectML 多 GPU 设备路由脚本

请确认优先交付项，我将按工程规范输出可集成代码片段与测试用例。

---
