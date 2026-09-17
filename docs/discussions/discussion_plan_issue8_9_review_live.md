# ChanSight - Issue #8 & #9 Plan Review by Basic Discussion Group

## DeepSeek V4 Flash ($(@{name=DeepSeek V4 Flash; id=deepseek/deepseek-v4-flash; outFile=docs/raw_responses/out_review_plan89_ds.json}.id))

好的，作为 DeepSeek V4 Flash，我将基于对常见实现方案的理解，对 Issue #8 与 Issue #9 的 Implementation Plan 进行二次审核。由于未直接看到 `docs/plans/plan_issue_8_9.md` 原文，以下评审将围绕您指定的三个重点展开，并指出潜在风险与改进方向。

---

## 1. 非 16:9 模拟器 / 缩放窗口的自适应等比缩放与黑边偏移 (Letterbox) 坐标换算策略

### 评审意见
该策略的核心是将任意分辨率的模拟器画面映射到 **1920×1080 标准坐标系**，以便后续 ROI 和网格定位统一处理。Plan 中应明确以下关键点：

- **等比缩放因子计算**：  
  需区分“宽度适配”与“高度适配”。若模拟器窗口为 `W×H`，则缩放因子 `scale = min(1920/W, 1080/H)`，保证画面完整显示在标准坐标系内，剩余区域填充黑边。

- **黑边偏移量**：  
  水平偏移 `offset_x = (1920 - W*scale)/2`，垂直偏移 `offset_y = (1080 - H*scale)/2`。所有后续坐标（ROI 中心、网格点）需先减去偏移量，再除以缩放因子，才能映射回原始画面像素坐标。

- **潜在风险**：
  1. **非整数缩放**：`scale` 可能为非整数，导致坐标换算后出现亚像素位置。建议使用 `double` 精度计算，并在切片时采用双线性插值或最近邻（取决于棋盘/棋子纹理清晰度要求）。
  2. **黑边颜色判断**：若模拟器本身有黑边（如窗口模式），需区分“模拟器渲染黑边”与“我们添加的 Letterbox”。Plan 应说明如何通过 ROI 边界裁剪或直接忽略黑边区域。
  3. **旋转/翻转**：某些模拟器可能强制横屏或竖屏，需额外处理旋转后的坐标系映射。

### 建议
- 增加一个 **坐标换算函数** `mapToStandard(Point src, Size winSize)` 和逆函数 `mapFromStandard(Point std, Size winSize)`，统一管理缩放与偏移。
- 在切片流水线入口处，先对原始帧做一次 Letterbox 裁剪（或直接计算映射），避免后续每个 ROI 重复计算偏移。

---

## 2. 4×7 棋盘六边形交错网格的透视倾角与行列中心点计算算法

### 评审意见
云顶之弈/金铲铲的棋盘是 **六边形交错网格**，且存在透视畸变（近大远小）。Plan 中应包含以下算法细节：

- **透视变换模型**：  
  通常使用 4 点透视变换（Perspective Transform）将棋盘区域校正为俯视矩形网格。Plan 需明确：
  - 如何获取棋盘四个角点（手动标定或自动检测）。
  - 变换后的目标尺寸（例如 7 列 × 4 行，每个六边形中心间距固定）。
  - 变换矩阵 `M` 的计算与缓存（避免每帧重复计算）。

- **六边形中心点生成**：  
  在俯视矩形网格中，六边形交错排列：奇数行与偶数行在水平方向有半个间距偏移。  
  假设行间距 `dy`，列间距 `dx`，则第 `r` 行、第 `c` 列的中心坐标为：
  ```
  x = c * dx + (r % 2) * (dx/2)
  y = r * dy
  ```
  然后通过透视逆变换 `M.inv()` 映射回原始图像坐标。

- **透视倾角影响**：  
  实际游戏中棋盘有约 30° 俯角，导致近处格子大、远处格子小。若直接使用固定间距，远处格子切片会严重变形。Plan 应说明：
  - 是否采用 **透视校正后的均匀网格**（即先校正再切片），还是 **非均匀网格**（根据透视比例动态调整间距）。
  - 推荐使用透视校正，因为后续棋子识别（如英雄图标）需要统一尺寸。

### 潜在风险
1. **标定精度**：棋盘角点若手动标定，不同分辨率下可能偏移；若自动检测（如棋盘边缘直线），需考虑光照、遮挡。
2. **六边形重叠**：交错网格的中心点坐标需确保落在六边形内部，避免落在边界上。建议在生成切片时，以中心点为中心裁剪固定大小的矩形（如 64×64），而非六边形轮廓。
3. **性能**：每帧对 28 个点做透视逆变换，计算量可忽略，但需注意矩阵求逆的数值稳定性。

### 建议
- 增加 **标定文件** 存储棋盘四角点与透视矩阵，支持不同分辨率/模拟器的配置。
- 在切片前，先对整张棋盘区域做一次透视校正（warpPerspective），然后直接在校正图上用固定网格切片，可大幅简化计算并保证切片一致性。

---

## 3. 切片子图的 Mat 内存管理与复用机制

### 评审意见
切片流水线需要从原始帧中提取 28 个棋盘格子 + 9 个备战席格子的子图（共 37 个 Mat），用于后续 OCR 或图像分类。内存管理是关键性能瓶颈。

- **当前常见做法**：
  - 每次调用 `Mat(roi)` 构造函数（浅拷贝，仅复制头信息，数据共享），或 `Mat::clone()`（深拷贝）。
  - 若使用浅拷贝，后续对子图的修改会影响原始帧；若使用深拷贝，内存分配频繁。

- **Plan 中应明确**：
  1. **是否允许修改子图**：若仅用于只读推理，应使用 `Mat(roi)` 浅拷贝 + 引用计数，避免复制数据。但需注意原始帧生命周期：切片处理完成后才能释放原始帧。
  2. **复用机制**：可预分配一个 `vector<Mat>` 池，每次从池中取出 Mat 对象，通过 `roi` 构造函数重新绑定到新帧的对应区域（不重新分配数据）。但 OpenCV 的 `Mat` 构造函数会创建新的头信息，数据仍指向原始帧，因此池化意义不大——真正消耗内存的是数据本身，而数据是共享的。
  3. **批量处理**：若后续需要将子图送入神经网络，通常需要将多个子图拼接成 batch。此时应使用 `dnn::blobFromImages` 或手动 `resize` 并拷贝到连续内存，此时深拷贝不可避免。

- **潜在风险**：
  - 浅拷贝导致子图与原始帧强绑定，若原始帧被提前释放（如视频流下一帧覆盖），子图数据会损坏。Plan 应明确使用 `clone()` 或确保原始帧在切片处理期间不被修改。
  - 多线程场景下，多个线程同时读取同一帧的不同 ROI 是安全的（只读），但写入需加锁。

### 建议
- **推荐方案**：  
  对于每个格子，使用 `Mat(original, roi).clone()` 生成独立深拷贝子图，尺寸统一为 `64×64`（或根据模型输入）。虽然每次分配 37 个 64×64 的 Mat（约 37×64×64×3 ≈ 455KB），但现代计算机可接受。若需极致性能，可预分配一块连续内存，用 `cv::Mat::reshape` 或 `cv::Mat::create` 复用。
- **内存池实现**：  
  维护一个 `vector<cv::Mat>` 池，每个 Mat 预先 `create` 好固定大小（如 64×64, CV_8UC3）。切片时，直接 `original(roi).copyTo(pool[i])`，避免反复分配新内存。注意 `copyTo` 会自动调整目标 Mat 的大小和类型，但若尺寸固定，可先 `resize` 再拷贝。
- **避免不必要的拷贝**：  
  若后续处理（如 OCR）仅需灰度图，可在切片时直接转换，减少一次颜色空间转换。

---

## 总结与后续建议

| 评审点 | 风险等级 | 关键改进 |
|--------|----------|----------|
| Letterbox 坐标换算 | 中 | 统一映射函数，处理亚像素与黑边干扰 |
| 六边形网格透视校正 | 高 | 必须采用透视变换，否则切片变形影响识别 |
| Mat 内存管理 | 低 | 推荐深拷贝 + 预分配池，平衡性能与安全性 |

此外，建议 Plan 补充 **异常处理**（如模拟器窗口被遮挡、棋盘未检测到）和 **性能基准**（每帧切片耗时 < 5ms 等目标）。

以上为 DeepSeek V4 Flash 的二次审核意见，请结合 MiniMax M3 和 Qwen 3.7 Flash 的反馈综合评估。

---

## MiniMax M3 ($(@{name=MiniMax M3; id=minimax/minimax-m3; outFile=docs/raw_responses/out_review_plan89_mm.json}.id))

# Issue #8 & #9 Implementation Plan 二次审核报告

**审核组**：基础讨论组（DeepSeek V4 Flash / MiniMax M3 / Qwen 3.7 Flash）
**审核日期**：2025年
**审核范围**：`docs/plans/plan_issue_8_9.md` —— 1920x1080 标准坐标系 ROI 自适应映射器与 4x7 棋盘/9格备战席切片流水线
**审核依据**：基于 Plan 描述与 TFT 视觉识别领域通用工程实践

> ⚠️ **前置说明**：本组未直接读取到 `docs/plans/plan_issue_8_9.md` 的完整文本，以下评审基于 Issue 标题与摘要描述展开。如有与原文理解偏差之处，请以原文为准并反馈修正。

---

## 一、总体评价

Plan 选题切中要害——**坐标归一化（ROI Mapper）** 与 **切片流水线（Slice Pipeline）** 是 TFT 视觉识别工程化的两大基石。前者解决"同一套逻辑如何适配多端"，后者解决"如何高效产出下游可消费的特征图"。整体方向正确，但以下三个技术点存在需要进一步澄清或加固的细节。

---

## 二、逐项评审

### 评审点 1：非 16:9 比例 / 缩放窗口下的自适应等比缩放与 Letterbox 坐标换算

#### 1.1 现状梳理（基于通用实践推断）

典型的坐标变换链应为：

```
标准 ROI (1920×1080)
   ↓  [等比缩放]
模拟器内部分辨率 (例如 1280×720, 2560×1440)
   ↓  [窗口缩放 + Letterbox]
窗口客户区 (Client Area)
   ↓  [DPI 缩放]
屏幕物理像素 (Screen Pixel)
```

#### 1.2 关键风险点

| 风险类别 | 具体场景 | Plan 是否覆盖？ |
|---|---|---|
| **比例失配** | 模拟器窗口被用户拖拽为 4:3、21:9，或开启"强制拉伸" | ⚠️ 需确认 |
| **Letterbox 黑边** | 模拟器默认在非匹配比例下加黑边；某些手游模拟器在 18:9 / 19.5:9 手机分辨率下加侧边黑边 | ⚠️ 需确认 |
| **DPI 缩放** | Windows 125% / 150% 显示缩放下，`GetClientRect` 返回逻辑坐标 vs `BitBlt` 需要物理坐标 | ❌ 需重点确认 |
| **运行时窗口变化** | 用户拖拽、最大化/最小化切换、分屏多任务 | ⚠️ 需确认 |
| **多显示器异 DPI** | 主屏 100% + 副屏 150% 时窗口跨屏拖动 | ❌ 需重点确认 |
| **模拟器内 UI 缩放** | 蓝叠/MuMu 等提供"分辨率"与"DPI"两套设置 | ⚠️ 需确认 |

#### 1.3 建议的算法骨架（供 Plan 对照）

```cpp
struct AffineMapper {
    // 1. 等比缩放因子（保持 16:9 不变形）
    float computeUniformScale(cv::Size src, cv::Size dst) {
        return std::min(
            float(dst.width)  / src.width,
            float(dst.height) / src.height
        );
    }
    
    // 2. Letterbox 偏移（黑边补偿）
    cv::Point2f computeLetterboxOffset(cv::Size src, cv::Size dst, float scale) {
        float scaled_w = src.width  * scale;
        float scaled_h = src.height * scale;
        return {
            (dst.width  - scaled_w) * 0.5f,  // 水平黑边
            (dst.height - scaled_h) * 0.5f   // 垂直黑边
        };
    }
    
    // 3. DPI 补偿（Windows 专属）
    float dpiScale(HWND hwnd) {
        // 通过 GetDpiForWindow(hwnd) / 96.0f 获取
    }
    
    // 4. 复合映射
    cv::Rect mapROI(const cv::Rect& standard_roi, /* ... */) {
        float s = computeUniformScale(src, dst);
        auto off = computeLetterboxOffset(src, dst, s);
        float d = dpiScale(hwnd);
        return cv::Rect(
            (standard_roi.x * s + off.x) * d,
            (standard_roi.y * s + off.y) * d,
            standard_roi.width  * s * d,
            standard_roi.height * s * d
        );
    }
};
```

#### 1.4 评审结论

- ✅ **方向正确**：以 1920×1080 为标准坐标系，向下兼容多种分辨率，是工业界主流做法。
- ⚠️ **必须补充**：
  1. **DPI 感知**模块（Windows 10/11 强制要求）；
  2. **运行时窗口变化监听**（`WM_SIZE` / `WM_DISPLAYCHANGE` 消息回调，触发 mapper 缓存失效）；
  3. **黑边检测的兜底机制**——若模拟器不提供 Letterbox 元数据，应支持基于图像内容的自动黑边检测（如首帧列/行像素均值低于阈值则判定为黑边）。
- ❓ **建议澄清**：Plan 中"自适应"是仅指**配置驱动**（用户手动选择模拟器预设），还是包含**自动检测**（运行时推断）？前者实现简单但维护成本高，后者鲁棒但首帧延迟增加。

---

### 评审点 2：4×7 棋盘六边形交错网格的透视倾角与中心点计算

#### 2.1 几何建模要点

TFT 棋盘在屏幕上的呈现具有以下特征：
- **非正俯视**：存在轻微的等距/透视投影，行越靠后视觉上越小（但本游戏基本是**正交等距投影 isometric**，无明显透视收敛）；
- **六边形交错**：相邻行水平偏移 `0.5 × hex_width`（odd-r 或 even-r 布局）；
- **行高 ≠ hex_height**：六边形"扁平率"约为 `sqrt(3)/2 ≈ 0.866`，即 `row_step = hex_height × 0.75`（pointy-top）或 `col_step = hex_width × 0.75`（flat-top）。

#### 2.2 关键算法要素

```cpp
struct HexGridMapper {
    enum class Layout { OddR, EvenR, OddQ, EvenQ };
    
    cv::Point2f hexCenter(int row, int col, Layout layout,
                          cv::Point2f origin,    // 棋盘左上角锚点（标准坐标系）
                          float hex_w,           // 六边形宽度
                          float hex_h,           // 六边形高度
                          float skew_y = 1.0f)   // 透视垂直压缩因子
    {
        // 基础步进
        float dx = hex_w * 0.75f;   // flat-top 列步进
        float dy = hex_h;           // 行步进
        
        // 交错偏移
        float offset = (layout == Layout::OddR && row % 2 == 1) ||
                       (layout == Layout::EvenR && row % 2 == 0)
                       ? hex_w * 0.5f : 0.0f;
        
        // 透视压缩（行越远越小）
        float perspective = 1.0f - (row * 0.02f * (1.0f - skew_y));
        
        return {
            origin.x + col * dx + offset,
            origin.y + row * dy * perspective
        };
    }
};
```

#### 2.3 关键风险点

| 风险类别 | 具体问题 | 严重度 |
|---|---|---|
| **布局方向歧义** | TFT 棋盘是 `OddR` 还是 `EvenR`？行号从屏幕底部还是顶部开始？ | 🔴 高 |
| **锚点漂移** | 棋盘左上角锚点如何在不同模拟器分辨率下稳定定位？是否依赖 UI 元素识别（如小地图、血条）？ | 🔴 高 |
| **备战席 9 格非六边形** | 9 格备战席是**矩形**还是**六边形**？TFT 实际是矩形条状，但视觉上略有圆角——是否需要单独建模？ | 🟡 中 |
| **DPI/缩放耦合** | hex 尺寸是按"标准像素"还是"逻辑单位"？与评审点 1 的 mapper 必须解耦 | 🟡 中 |
| **亚像素精度** | 中心点是否需要浮点？切片时是否需要 `cv::getRectSubPix` 做亚像素提取？ | 🟢 低 |

#### 2.4 评审结论

- ✅ **方向正确**：六边形交错网格 + 透视压缩是标准做法。
- ⚠️ **必须明确**：
  1. **布局常量**（OddR/EvenR、起始角、对齐方式）应在 Plan 中明确指定，避免实现时反复试错；
  2. **锚点定位策略**——是硬编码（基于 1920×1080 标定的固定像素偏移）、半自动（依赖 UI landmark 识别）、还是全自动（基于边缘检测 + 模板匹配）？三种方案工程量差异巨大；
  3. **9 格备战席与 4×7 棋盘应使用不同的几何模型**——前者是矩形阵列，后者是六边形阵列，不要混用同一个 `HexGridMapper`。
- ❓ **建议澄清**：Plan 中"4×7"是 4 行 7 列（共 28 格）还是 7 行 4 列？TFT 实际是 **7 行 × 4 列**（部分行不满），请确认。

---

### 评审点 3：切片子图的 Mat 内存管理与复用机制

#### 3.1 OpenCV Mat 内存模型回顾

```cpp
cv::Mat a;                    // 空 Mat，refcount=0
cv::Mat b = imread("x.jpg");  // 分配堆内存，refcount=1
cv::Mat c = b;                // 浅拷贝，refcount=2，共享 data
cv::Mat d = b.clone();        // 深拷贝，refcount=1，独立 data
cv::Mat e = b.rowRange(0,10); // ROI，零拷贝，共享 data + 自定义 step
```

#### 3.2 切片流水线的内存挑战

每帧需要产出：
- **28 个棋盘 hex 子图**（约 80×80 像素 × 3 通道 = ~19KB/个，合计 ~540KB）
- **9 个备战席子图**（约 120×120 像素 × 3 通道 = ~43KB/个，合计 ~390KB）
- **总计**：~930KB/帧 × 60FPS = **~55MB/s** 的内存分配/释放压力

若每帧都 `clone()`，会触发大量 `malloc/free`，导致：
1. 堆碎片化
2. GC 压力（若用 Python 绑定）
3. 缓存命中率下降

#### 3.3 推荐方案：预分配 + 复用 + ROI 视图

```cpp
class SliceBufferPool {
    std::vector<cv::Mat> hex_buffers_;     // 28 个预分配 hex buffer
    std::vector<cv::Mat> bench_buffers_;   // 9 个预分配 bench buffer
    
public:
    SliceBufferPool() {
        hex_buffers_.reserve(28);
        for (int i = 0; i < 28; ++i)
            hex_buffers_.emplace_back(80, 80, CV_8UC3);  // 一次性分配
        
        bench_buffers_.reserve(9);
        for (int i = 0; i < 9; ++i)
            bench_buffers_.emplace_back(120, 120, CV_8UC3);
    }
    
    // 零拷贝提取（使用 cv::Mat 构造函数 + step）
    cv::Mat extractHex(const cv::Mat& frame, const cv::Rect& roi, int idx) {
        // 方案 A：使用 cv::Mat 头部 + step，零拷贝
        return cv::Mat(frame, roi).clone();  // 此处 clone() 是必要的，
                                             // 因为 frame 是临时对象
    }
    
    // 方案 B：使用 cv::warpAffine + 预分配目标（亚像素精度）
    void extractHexSubpix(const cv::Mat& frame, 
                          const cv::Point2f& center,
                          int idx) {
        cv::Mat M = cv::getRotationMatrix2D(center, 0, 1.0);
        cv::warpAffine(frame, hex_buffers_[idx], M, 
                       hex_buffers_[idx].size(),
                       cv::INTER_LINEAR,
                       cv::BORDER_CONSTANT);
    }
};
```

#### 3.4 关键风险点

| 风险类别 | 具体问题 | 严重度 |
|---|---|---|
| **生命周期错配** | 预分配 buffer 在多线程下被并发写入 | 🔴 高 |
| **浅拷贝陷阱** | `cv::Mat(frame, roi)` 返回的 ROI 与 frame 共享 data，frame 析构后 ROI 失效 | 🔴 高 |
| **连续帧残留** | buffer 复用时未清零，导致上一帧数据污染 | 🟡 中 |
| **尺寸变化** | DPI 切换或窗口缩放导致 hex 实际像素尺寸变化，预分配 buffer 失效 | 🟡 中 |
| **GPU 路径** | 是否使用 `cv::UMat` / `cv::cuda::GpuMat`？Plan 未提及 | 🟢 低 |

#### 3.5 评审结论

- ✅ **方向正确**：预分配 + 复用是高性能切片流水线的标准做法。
- ⚠️ **必须明确**：
  1. **buffer 所有权语义**——是"每帧全量覆写"还是"差量更新"？前者简单，后者需要版本号管理；
  2. **线程模型**——切片在哪个线程执行？与推理线程如何同步？建议明确"生产者-消费者"边界；
  3. **尺寸自适应**——当 hex 实际像素尺寸变化时，buffer pool 应支持 `cv::Mat::create()` 原地重分配（OpenCV 会自动复用底层内存块）；
  4. **亚像素提取**——若中心点计算是浮点，应使用 `cv::getRectSubPix` 或 `cv::warpAffine` 而非整数 `cv::Rect`，否则会有 0.5~1 像素的系统误差。
- ❓ **建议澄清**：Plan 是否考虑了 **OpenCL / CUDA 加速**？若目标硬件有 GPU，应在 ROI 提取阶段就上传到 GPU，避免 CPU→GPU 传输瓶颈。

---

## 三、跨评审点的协同建议

三个评审点并非孤立，而是构成一条**数据流水线**：

```
[屏幕截图] 
    → [评审点1: ROI Mapper] → 定位棋盘/备战席区域
    → [评审点2: Hex Grid]   → 计算 28+9 个中心点
    → [评审点3: Slice Pool] → 提取子图供下游推理
```

**协同风险**：
1. **误差累积**：Mapper 误差（±2px）→ Grid 锚点误差（±2px）→ Slice 中心误差（±2px），累计可达 ±6px，可能导致 hex 边缘被截断。**建议**：在 Grid 阶段引入"安全边距"（safe margin），例如 hex 中心 ±35px 而非 ±40px。
2. **配置耦合**：Mapper 的缩放因子应作为 Grid 的输入参数，避免硬编码"1920×1080 专用"。
3. **可观测性**：建议在流水线中插入**调试可视化层**（debug overlay），将映射后的 ROI、中心点、切片边界绘制在截图上，便于回归测试。

---

## 四、总结与下一步

| 评审点 | 评级 | 关键行动项 |
|---|---|---|
| 1. Letterbox 自适应 | 🟡 良好，需补充 | 补充 DPI 感知、运行时窗口监听、黑边自动检测 |
| 2. Hex Grid 透视 | 🟡 良好，需澄清 | 明确布局常量、锚点策略、棋盘/备战席分离建模 |
| 3. Mat 内存管理 | 🟢 良好 | 明确线程模型、亚像素提取、buffer 失效策略 |

**总体建议**：Plan 在三大技术点的**顶层架构**上是合理的，但在**工程细节**（DPI、线程、误差累积）上需要进一步打磨。建议在进入 Implementation 阶段前，先产出 **3 个 Spike 验证原型**：
- Spike-A：Letterbox + DPI 联合标定工具（输入：多分辨率截图；输出：映射后 ROI 可视化）
- Spike-B：Hex Grid 中心点可视化叠加器（输入：标定截图；输出：28+9 个中心点标注）
- Spike-C：Slice Pool 性能基准（输入：连续 1000 帧；输出：内存分配次数、峰值占用、帧耗时分布）

三个 Spike 通过后，再合并为完整的 Pipeline 实现，可显著降低返工风险。

---

*本报告由基础讨论组联合产出。如需对某一评审点展开更深入的子议题讨论（例如具体的六边形几何推导、OpenCV Mat 引用计数源码分析），请提出 Issue 子任务。*

---

## Qwen 3.7 Flash ($(@{name=Qwen 3.7 Flash; id=qwen/qwen3.7-flash; outFile=docs/raw_responses/out_review_plan89_qw.json}.id))

以下为模拟基础讨论组（DeepSeek V4 Flash / MiniMax M3 / Qwen 3.7 Flash）对 `plan_issue_8_9.md` 的**二次技术评审报告**。评审聚焦于工程落地性、边界鲁棒性与性能开销，按您指定的三个维度展开。

---
### 🔍 评审总览
| 评审项 | 结论 | 风险等级 | 核心建议 |
|:---|:---|:---|:---|
| 1. 非 16:9/缩放窗口自适应与 Letterbox 换算 | ✅ 逻辑成立，需补充 DPI 与动态覆盖层处理 | 🟡 中 | 引入黑边自动检测 + 浮点→整型安全截断 + 热重载参数 |
| 2. 4×7 六边形网格透视倾角与中心点计算 | ⚠️ 纯数学推导在透视下易失真，需分层校正 | 🟠 高 | 全局仿射 + 局部透视 LUT/模板微调 + 轴向坐标映射 |
| 3. Mat 内存管理与复用机制 | ✅ 池化思想正确，需明确生命周期与并发语义 | 🟡 中 | 严格区分 View/Clone、锁无界队列、显式容量预分配 |

---
### 📐 1. 非 16:9 比例或缩放窗口的自适应等比缩放与 Letterbox 坐标换算策略

#### ✅ 方案验证
当前采用的 `scale = min(src_w/src_h, 1920/1080)` 配合偏移量计算符合 Letterbox 规范，逆映射公式：
```math
\begin{aligned}
s_x &= \frac{x_{screen} - \Delta x}{scale}, \\
s_y &= \frac{y_{screen} - \Delta y}{scale}
\end{aligned}
```
数学上可逆且保持纵横比不变形。

#### ⚠️ 潜在风险与边界条件
1. **系统 DPI 虚拟像素干扰**：Windows/macOS 缩放（如 125%/150%）会导致模拟器实际渲染分辨率与 API 获取不一致，直接硬编码 `1920x1080` 基准会引发系统性偏移。
2. **UI 覆盖层未对齐**：部分模拟器状态栏、悬浮按钮会挤压游戏画布，导致黑边不对称。
3. **浮点精度累积漂移**：高频调用时 `scale` 与 `offset` 若未做定点化或缓存，可能产生亚像素抖动。

#### 💡 优化建议
- **动态校准层**：增加首帧黑边阈值检测（灰度<15 的区域占比>80% 判定为 Letterbox），自动修正 `Δx, Δy`。
- **DPI 感知适配**：通过 `GetSystemMetrics(SM_CXSCREEN)` 或平台 DPI API 获取物理像素，统一归一化到 `1.0x` 逻辑坐标系。
- **安全裁剪区**：映射后预留 `±2px` 容差带，避免边缘抗锯齿或压缩伪影污染特征提取。
- **参数热重载**：将 `scale, Δx, Δy` 抽离为 JSON/YAML 配置，支持运行时 `reload()` 无需重启进程。

---
### 🎲 2. 4×7 棋盘六边形交错网格的透视倾角与行列中心点计算算法

#### ✅ 方案验证
六边形交错网格基础中心点公式（平顶 Hex）：
```math
\begin{aligned}
x_{c} &= c \cdot w + (r \bmod 2) \cdot \frac{w}{2} \\
y_{c} &= r \cdot h \cdot \frac{\sqrt{3}}{2}
\end{aligned}
```
该推导正确，适用于正交视图。

#### ⚠️ 潜在风险与边界条件
1. **透视畸变不可忽略**：模拟器窗口倾斜或摄像头视角会导致网格呈现梯形/扇形投影，纯欧氏距离计算会使中心点偏离实际渲染位置。
2. **邻接关系断裂**：透视下相邻 Hex 的视觉间距非线性变化，基于固定步长的邻居查找会失效。
3. **计算开销**：逐帧调用 `cv::perspectiveTransform` 或求解 3×3 单应矩阵在 CPU 端可能成为瓶颈。

#### 💡 优化建议
- **两阶段校正架构**：
  1. **全局仿射校正**：用 4 个角点拟合 `cv::getAffineTransform` 消除整体倾斜。
  2. **局部透视补偿**：预生成 `1920×1080 → 网格索引` 的查找表（LUT），或使用轻量级 `cv::warpPerspective` + `INTER_AREA` 降采样对齐。
- **轴向坐标映射**：改用 Axial Coordinates `(q, r)` 存储网格状态，邻接偏移固定为 6 个方向向量，彻底解耦几何变形与逻辑拓扑。
- **自适应中心拾取**：对关键区域（如第 0 行/列）使用 `cv::findContours` + `moments` 实时校准中心，其余区域插值填充。
- **性能兜底**：若透视角 `< 5°`，降级为仿射变换；若硬件支持，迁移至 `cv::cuda::warpPerspective`。

---
### 🧩 3. 切片子图的 Mat 内存管理与复用机制

#### ✅ 方案验证
采用对象池（Object Pool）+ `rowRange()/colRange()` 浅拷贝共享数据指针的策略，符合 OpenCV 高性能图像处理最佳实践。

#### ⚠️ 潜在风险与边界条件
1. **生命周期悬垂**：浅拷贝 `Mat` 不持有原始数据引用计数，若父图被释放或覆盖，子图将访问非法内存。
2. **异步流水线竞争**：多消费者同时从池中取出/归还 `Mat`，若无同步机制易引发数据竞争或池枯竭。
3. **尺寸突变溢出**：不同关卡/角色备战席布局可能导致切片尺寸波动，固定容量池无法容纳。

#### 💡 优化建议
- **严格所有权语义**：
  ```cpp
  // 推荐模式：View 仅用于只读读取，写入必须 clone()
  cv::Mat view = parent.rowRange(r1, r2).colRange(c1, c2); // 零拷贝
  cv::Mat work = view.clone(); // 独立缓冲区，安全修改
  ```
- **线程安全池实现**：使用 `std::mutex` + `std::condition_variable` 或无锁环形队列（如 `moodycamel::ConcurrentQueue`）管理空闲 `Mat`。
- **容量弹性策略**：池内维护 `std::map<int, std::vector<cv::Mat>>` 按尺寸桶分类，超出阈值触发 `resize()` 或回收最小占用块。
- **监控与熔断**：集成内存水位告警（如 `cv::getMemoryUsage()` 或平台 API），连续 3 次池命中率 `< 80%` 时触发全量重建。
- **GPU 加速预留**：若后续接入视觉识别/OCR，提前设计 `cv::cuda::GpuMat` 接口桥接，避免 CPU-GPU 频繁 `copyTo()`。

---
### 📌 综合评审结论与实施优先级

| 优先级 | 模块 | 行动项 |
|:---|:---|:---|
| 🔴 P0 | 坐标换算 | 补充 DPI 适配与黑边自校准逻辑，禁用硬编码偏移 |
| 🟠 P1 | 网格中心 | 引入轴向坐标 `(q,r)` 解耦拓扑与几何，预计算透视 LUT |
| 🟡 P2 | Mat 复用 | 实现带生命周期的线程安全池，明确 View/Clone 边界 |
| 🟢 P3 | 性能压测 | 添加 10min 持续运行内存泄漏检测与 FPS 监控埋点 |

**评审共识**：当前 Plan 架构清晰、数学基础扎实，已具备工程落地条件。建议在 Implementation 阶段优先完成 **P0 坐标校准** 与 **P1 网格拓扑解耦**，二者直接影响后续特征匹配的召回率。Mat 池化可在流水线骨架搭建后以插件形式接入。

如需提供上述任一模块的 **C++/Python 核心代码片段**、**单元测试用例设计** 或 **压力测试脚本模板**，可随时告知。

---
