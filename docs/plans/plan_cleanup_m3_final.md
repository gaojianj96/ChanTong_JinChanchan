# 转型冲刺正式计划 v2：清债 + 视觉数据资产 + M3 起步 (Final DAG)

> Planner: DeepSeek V4 Pro 0813 ｜ 日期: 2026-09-12 ｜ 状态: **待用户批准**（5 分钟窗口 + Opus 代判适用）
> 上游: 草案 `plan_cleanup_m3_dag_draft.md` + 委员会研讨 `docs/discussions/discussion_cleanup_m3_dag_live.md`

## 1. Planner 仲裁记录（对委员会意见的去重与裁定）

| # | 委员会意见（来源） | 裁定 | 处置 |
|---|---|---|---|
| 1 | B1-B5 验收无量化指标（DS 需求） | **Blocker** | 各任务补验收矩阵（见 §3） |
| 2 | A3 单一 3px 阈值在 3840x2088 非等比缩放下失效（DS + Qwen R2） | **Blocker** | 改分轴阈值 x≤6px / y≤5.8px（≡3px@1080p 分轴）+ 四角点独立判定 + 边缘 10% 加严 |
| 3 | C1 过粗违反单一职责，阻塞 M3 关键路径（MiniMax P1-1 + DS） | **Blocker** | 拆 C1a（几何元数据 Schema，依赖 A3）与 C1b（服务契约 v0，可立即开工） |
| 4 | C3 仅依赖服务契约，不应被 C2 串行阻塞（MiniMax P1-2） | **Blocker** | DAG 重排：C3 依赖 C1b；C2 依赖 C1a+C1b |
| 5 | C5/C6 共享后处理未建模（MiniMax P1-3） | **Important** | 新增 C4.5 PostProcessor 抽象（NMS/ConfFilter/ROICrop），C5/C6 并行复用 |
| 6 | Golden Set 场景坍缩风险，676 帧可能同质（Qwen R1） | **Blocker** | A1→A2 增加多样性门禁；不达标须用户补录 ≥2 个异构会话 |
| 7 | B1/B3 共享 D3D 生命周期、B2/B5 竞争文件句柄（Qwen R3） | **Important** | B1↔B3 串行对；B2↔B5 文件锁互斥；B4 独立可并行 |
| 8 | A1 不必全量双模型（DS）/ 双层仲裁协议缺失（Qwen M5） | **Important** | 维持用户已定双层结构（全量只用便宜 Flash）；分歧帧 → 人工裁决写入 A1 SOP |
| 9 | C2 验收须含 A1 未覆盖新场景（Qwen M4） | **Important** | 写入 C2 验收标准 |
| 10 | Recorder B 改动可能破坏既有产物（MiniMax P2-1） | **Important** | A1 期间冻结既有数据目录只读；B 系列独立分支开发，合并前全量回归 |
| 11 | Anchor 内嵌 ML 会造成 Vision 内循环依赖（MiniMax P2-2） | **Important** | AnchorDetector 用经典 CV（无 ML）；ML 精修留 M3 后期可选 |
| 12 | DI 一次性接入爆炸半径大（MiniMax P2-3） | **Important** | C3 先注册 StubInferenceEngine + CLI `--vision-dry-run`，C4 后替换真实实现 |
| 13 | A1"坏帧"口径/审批边界未定义（DS） | **Important** | 坏帧四分类 + Opus 代判例外清单（见 §4） |
| 14 | C4-C6 验收空泛（DS） | **Important** | 补延迟/内存/回退量化指标（见 §3） |

## 2. 任务 DAG v2

### Sprint A：视觉数据资产

| id | 任务 | depends_on | 验收标准（量化） | 负责人 |
|---|---|---|---|---|
| A1 | 双层视觉核验 676 帧 | - | bulk 报告：坏帧四分类（全黑/模糊/花屏/重复候选）+ 场景分类 + **多样性矩阵（人口数/装备数/阶段分布）**；spot 报告：关键帧+5~10% 抽校结论；分歧帧人工裁决 SOP | 视觉核验员 |
| A2 | Golden Dataset 标注 | A1（**门禁**：多样性矩阵达标，否则用户补录 ≥2 异构会话） | 50~100 帧（覆盖全部出现场景类）标注 JSON：28 棋盘/9 备战/5 商店中心点 + 源分辨率 + 标定者；QA 抽校复核通过 | 用户标注 + 视觉核验员复核 |
| A3 | 网格切片几何验证 | A2 | 分轴误差 x≤6px / y≤5.8px（@3840x2088）；四角点独立判定；边缘 10% 区域加严；残差叠加可视化 + QA 报告 | Executor + 视觉核验员 |

### Sprint B：代码清债（B1/B3 串行，B2/B5 文件互斥，B4 并行；独立分支 + 合并前回归）

| id | 任务 | depends_on | 验收标准（量化） | 负责人 |
|---|---|---|---|---|
| B1 | 热键生命周期（H2） | B3 | 连按 F6 100 次无非法状态；退出后 UnregisterHotKey 残留=0（重新注册无冲突）；单测+集成测试通过 | Executor |
| B2 | 异常退出文件完整性（H3） | - | Ctrl+C/强杀后 MP4 可播放（moov 完整校验）；meta.json 完整且字段与实际一致；编码器 flush 超时保护 ≤5s | Executor |
| B3 | WGC/D3D 资源生命周期（H4） | - | 窗口关闭→优雅退出或自动重建；DeviceRemoved 检测+恢复；30min 录制句柄/显存无单调增长 | Executor |
| B4 | UI/捕获线程解耦（H1） | - | 高频录制控制台无撕裂/死锁；捕获回调阻塞时长 <1ms（采样统计）；状态经 Channel 单向传递 | Executor |
| B5 | 采样异步落盘（H5） | - | 采样不阻塞捕获（P99 帧间隔恶化 <5%）；有界队列积压上限策略 + 磁盘满失败可恢复；文件名含 ms 时间戳 | Executor |

### Sprint C：M3 起步

| id | 任务 | depends_on | 验收标准（量化） | 负责人 |
|---|---|---|---|---|
| **C0** | canonical 几何基准修正：将已验证校准数据(`docs/qa_runs/a2/calibration_final.json`)写入 `CanonicalRoiDefinitions`（board 4×7 中心、bench 9、shop 5；修正旧行距 145→82、错位 65→48、BoardArea/BenchBounds 矩形） | -（A2 验证已完成） | GridSlicer/RoiMapper 单测更新至新基准全绿；A3 叠加验证 ≤3px@1920 基准 | Executor |
| C1a | 几何元数据 Schema（BoardGeometry：OriginAnchor/CellSize/ScaleFactor/AspectRatio） | A3 | Schema 文档 + 示例 JSON + 评审通过 | Tech Lead 设计 + Planner 仲裁 |
| C1b | M3 服务契约 v0（IFramePool/IInferenceEngine/IPostProcessor/IGridSlicer/IAnchorCalibrator 接口冻结） | - | 契约文档 + 接口 stubs 可编译；评审通过 | Tech Lead 设计 |
| C2 | Anchor 运行时校准骨架（经典 CV 角点检测，无 ML）+ 失败回退比例映射（confidence=low） | C1a, C1b | 3840x2088 实机帧校准后分轴误差达标；回退路径单测；**验收用例含 ≥1 个 A1 未覆盖新场景** | Executor |
| C3 | 代码缺口补齐：IFramePool 实现、Vision DI 注册（StubInferenceEngine）、CLI `--vision-dry-run` 接入 | C1b | DI 集成测试通过；dry-run 跑通全管道（无推理）；既有 147 项测试不回归 | Executor |
| C4 | ONNX 真实模型接入（DirectML 检测 + CPU 回退） | C3 | 空模型+实体模型推理链路跑通；回退日志正确；显存/内存无泄漏（连续 1000 次推理稳定） | Executor |
| C4.5 | PostProcessor 抽象（NMS/ConfFilter/ROICrop 基类） | C1b, C4 | 单元测试覆盖（NMS 锚框抑制、置信过滤、越界裁剪） | Executor |
| C5 | YOLO 后处理真实实现（替换 stub） | C1b, C4, C4.5 | 合成标注图 NMS 通过；单帧后处理 <8ms；输出强类型 DetectedUnit | Executor |
| C6 | PaddleOCR 单帧验证 | C1b, C4, C4.5 | 实机截图识别实测记录（准确率/延迟）；字典纠错单测通过；金币/阶段解析格式校验 | Executor |

## 3. 用户参与点（需要人工的事项汇总）

1. **批准本计划**（本次，5 分钟窗口 + Opus 代判）；
2. **A1→A2 门禁**：若多样性矩阵不达标 → 用户补录 ≥2 个异构会话（不同阵容/人口/分辨率）；
3. **A2 标注**：50~100 帧中心点人工标注（可分多次）；
4. **里程碑终验**。

## 4. 授权与安全/成本例外清单

- 授权：按 §2.8 执行（每 Executor 任务开工前申请；5 分钟无回应且非例外 → Opus 代判）；
- **安全例外（绝不代判）**：删除/覆盖既有数据集、系统级变更（注册表/服务/驱动）、凭据与密钥操作、发布与分发操作；
- **成本例外（绝不代判）**：新增付费依赖、单次模型调用预算估算 > $0.5、扩大模型用量预算、引入新模型角色；
- 本次计划内既定小额成本（委员会已发生的 $0.02 级调用、A1 双层核验估算 <$1）视为用户已批准范围内。