# 阶段确认记录: Sprint A+B 收官 (Stage Gate 2026-09-12)

> 委员会: DeepSeek V4.1 Flash / MiniMax M3 / Qwen 3.8 Flash ｜ Planner 裁决

## 委员会结论汇总

- DS V4.1 Flash: **有条件放行** —— M1 H1-H5 / M2 P0 已真实销账,证据链中上(视觉资产为抽校非全量人工真值,长尾风险)。
- MiniMax M3: **有条件放行** —— 分层/接口/测试硬指标通过;3 项 P1 须在 C1a/C2 前补齐。
- Qwen 3.8 Flash: **有条件放行** —— C4 须先出 go/no-go 判据与降级路径;A3 降级为 smoke 级;Golden 须独立人工复核 ≥30 帧。

## Planner 仲裁裁决

| # | 意见 | 裁定 | 处置 |
|---|---|---|---|
| 1 | IFrameDropSink 帧丢弃可观测性缺口(MiniMax P1-1) | **Blocker** | 并入 C1a Schema |
| 2 | provisional meta 孤儿态清理契约(MiniMax P1-2) | **Blocker** | 新增任务 **C0b**(Recorder 启动清扫/孤儿回收) |
| 3 | CAPTURE_LOST 端到端未验证(MiniMax P1-3) | **Important** | C3 验收新增端到端用例 |
| 4 | C4.5 必须前置于 C4(MiniMax) | **Blocker** | 顺序改为 C4.5 → C4 |
| 5 | C4 前置 go/no-go 判据: 延迟 <16ms@原生,否则降级 1080p ROI 推理(Qwen P0) | **Blocker** | C4 契约含判据 + 降级设计 |
| 6 | A3 仅 smoke 级;Golden 独立人工复核 ≥30 帧关键态(Qwen P1) | **Blocker** | A3 从 C 序列硬依赖降为并行验证;新增用户参与点(人工复核 ≥30 帧,视核预筛后工作量≈20min) |
| 7 | C2 需多 DPI/多显/非独占回归,容差 ≤2px(Qwen P1) | **Blocker** | C2 契约含三组回归用例 |
| 8 | C3 保留 frame_meta 扩展槽(Qwen P2) | **Important** | C3 契约注明 |
| 9 | A3 不卡 C 系列门禁(Qwen) | **Blocker** | C1a 依赖改为: C0 + A2 校准(已就绪) |
| 10 | 热键跨重启/Multi-インstance、遥测接入、订阅去重归档(MiniMax P2) | Optional | 入债务台账 D-07..09 |

## 放行决定: **GO(C 序列开工)**,执行顺序 v3

```
C0✅ → C0b(meta 孤儿回收) → C1a(几何 Schema + IFrameDropSink) → C2(Anchor 校准,多DPI回归)
     → C4.5(PostProcessor 抽象) → C4(ONNX 真模型 + go/no-go 判据 + 1080p 降级路径)
     → C3(IFramePool+DI+dry-run + CAPTURE_LOST 端到端 + frame_meta 扩展槽)
     → C5(目标检测后处理) ∥ C6(PaddleOCR 验证) → A3(切片叠加验证,smoke 级)
```

## 用户参与点(新增)

- **Golden 人工复核**: 视核预筛 ≥30 关键帧候选 → 用户确认(约 20 分钟,可延后至 C5 前);
- 多 DPI/多显回归测试需要用户切换显示配置配合(可延后,先用模拟 shell 探针)。