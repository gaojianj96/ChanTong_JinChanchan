# 转型冲刺计划草案：清债 + 视觉数据资产 + M3 起步 (Task DAG Draft)

> Planner: DeepSeek V4 Pro 0813 ｜ 日期: 2026-09-12 ｜ 状态: 草案待委员会研讨
> 上游依据: `docs/reviews/review_issue6_and_m1_final_summary.md`(H1-H5)、`docs/reviews/review_issues8_9_and_m2_final_summary.md`(P0 三项)、代码现状审计(2026-09-12)

## 1. 需求摘要 (Phase 0 输出)

**目标**: 在进入 M3 视觉推理管线之前：(a) 销掉 M1/M2 验收挂账；(b) 把已有的 676 张实机帧转化为可验证的视觉数据资产；(c) 打通 M3 起步所需的地基（DI/CLI 接入/输入契约/推理引擎验证）。

**已确认的范围边界**:
- 不涉及自动操作（代打），仅辅助决策与可视化核验；
- 视觉核验采用双层结构（全量 Flash Vision Exp + 关键帧/抽样 QianTry 抽校）；
- 授权采用 5 分钟窗口 + Opus 代判规则（安全/成本事项除外）。

**实机数据发现（重要前提）**: 两个会话共 676 帧，分辨率 **3840x2088**（宽 2.0x 基准、高 1.933x，宽高比 1.839 ≠ 16:9）；该事实本身印证了 M2 评审 P0 对非 16:9/非等比缩放的担忧，Golden Dataset 采集条件已就绪。

## 2. 任务 DAG

三个冲刺（Sprint）: **A 视觉数据资产** → **B 代码清债**（与 A 并行）→ **C M3 起步**（依赖 A/B 的产出）。

### Sprint A：视觉数据资产（优先，低代码）

| id | 任务 | depends_on | 负责人 | 验收标准 |
|---|---|---|---|---|
| A1 | 双层视觉核验 676 帧（全量粗筛：场景分类/坏帧/黑边/重复候选；抽校：关键帧+5~10% 随机分层抽样） | - | 视觉核验员 | 产出 bulk/spot 两份 QA 报告；坏帧与黑边帧清单；每场景可用帧数量统计 |
| A2 | Golden Dataset 标注（从 A1 通过帧中选 50~100 帧，标定 28 棋盘格/9 备战席/5 商店槽中心点） | A1 | 用户人工 + 视图核验复核 | 标注 JSON（含帧路径、源分辨率、逐点像素坐标）；人工/QA 复核通过 |
| A3 | 网格切片几何验证（GridSlicer 对 Golden Set 切片，叠加可视化 → 视力核验对齐，量化几何误差） | A2 | Executor + 视觉核验员 | 95% 格点中心像素误差 < 3px @1080p（等比换算）；叠加图目检通过 |

### Sprint B：代码清债（M1 H1-H5 → 可并行执行）

| id | 任务 | depends_on | 负责人 | 验收标准 |
|---|---|---|---|---|
| B1 | 修复全局热键生命周期（H2）：专用消息泵线程、注册/注销可靠、防抖状态机 | - | Executor | 连按 F6 不进入非法状态；进程退出后热键无残留；相关测试通过 |
| B2 | 异常退出文件完整性（H3）：按序停止采样→flush 编码器→关文件→写 meta；MP4 文件头校验 | - | Executor | Ctrl+C/异常退出后产物可播放；meta.json 完整 |
| B3 | WGC/D3D 资源生命周期（H4）：订阅 Closed 事件、DeviceRemoved 检测与恢复、Dispose 链 | - | Executor | 目标窗口关闭时优雅退出或重建捕获；资源无泄漏 |
| B4 | UI/捕获线程解耦（H1）：Spectre Live 状态经 Channel 传递，UI 线程独占渲染 | - | Executor | 高频录制下控制台无撕裂；捕获回调不被 UI 阻塞 |
| B5 | 采样异步落盘（H5）：采样请求入有界队列，后台编码写盘；文件名含时间戳 | - | Executor | 采样不阻塞捕获管线；帧率抖动可接受 |

### Sprint C：M3 起步（依赖 A3/B 完成后开工）

| id | 任务 | depends_on | 负责人 | 验收标准 |
|---|---|---|---|---|
| C1 | M3 输入契约 Schema v1（切片元数据、质量字段、序列化示例，落实 M2 遗留 P0③） | A3 | Tech Lead 设计 + Planner 仲裁 | Schema 文档 + 示例 JSON；评审通过 |
| C2 | Anchor-Based 运行时校准骨架（M2 遗留 P0②）：棋盘角点/商店槽锚点仿射校准 + 失败回退比例映射（confidence=low 标记） | A3, C1 | Executor | 3840x2088 实机帧校准后误差达标；失败回退路径有测试 |
| C3 | 代码缺口补齐：IFramePool 实现、IOnnxInferenceEngine/IPaddleOcrService/IYoloDetectorService 注册 DI、`AddChanSightVision()` 接入 CLI | C1 | Executor | DI 集成测试通过；CLI 可解析全部 Vision 服务 |
| C4 | ONNX 真实模型接入验证：DirectML 设备检测、CPU 回退、无内存泄漏 | C3 | Executor | 空/真实模型推理链路跑通；设备回退日志正确 |
| C5 | YOLO 后处理真实实现（NMS/坐标还原/Confidence 过滤，替换空壳 stub） | C1, C4 | Executor | 单帧单图推理输出强类型 DetectedUnit；NMS 单元测试通过 |
| C6 | PaddleOCR 单帧验证（字典纠错 + 金币/阶段解析） | C1, C4 | Executor | 实机截图识别准确率实测记录；字典纠错测试通过 |

## 3. 授权计划

- Sprint A1 的视觉核验调用（极小成本）视为已授权执行；
- 各 Executor 任务开工前按 §2.8 申请授权（5 分钟窗口 + Opus 代判，安全/成本项除外）；
- 里程碑验收由用户终验。

## 4. 待委员会研讨的问题

1. DAG 依赖划分是否合理（尤其 C1 是否应拆分为"几何元数据"与"服务契约"两层）？
2. Sprint A 与 B 的并行节奏、Executor 任务粒度是否适合 Flash 执行？
3. 是否有遗漏的债（M1/M2 评审全文以外的隐患）与风险反例？