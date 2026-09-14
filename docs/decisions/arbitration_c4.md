# 仲裁记录: C4(ONNX 引擎延迟探针 + go/no-go 判据)双盲评审

> Planner ｜ 2026-09-12 ｜ Kimi + Sonnet 双盲(初判 Conditional No-Go,修复后 GO)

## 裁定

| # | 意见 | 来源 | 裁定 | 处置 |
|---|---|---|---|---|
| 1 | 设备状态污染: LoadModel/Probe 失败后持久降级 CPU,不重试偏好设备 | Sonnet-2 + Kimi P1 | **Blocker** | ✅ `_preferredDevice` 不可变;LoadModel 每次重试偏好链(新鲜尝试);Probe 本地选设备 |
| 2 | ProbeLatency 锁跨测量段 + _currentDevice 竞写 | Sonnet-3 | **Important** | ✅ 会话构造阶段锁定;测量段免锁;设备字段仅构造期写 |
| 3 | 探针无模型不可测(CI 缺口) | Sonnet-1 + Kimi | 条件 | 收录: probe 集成验证绑定"实机 receipt"(模型就位时执行);接收据= C5/C6 前置检查 |
| 4 | 动态维度强置 1 可能 shape mismatch | Sonnet-4 | Minor | 债务: probe 文档注明(仅占位张量,适用静态输入模型) |
| 5 | 降采样因子经验公式需标注 | Sonnet-5 | Minor | 已在 Note 标注 empirical |

## 终局: C4 代码合并 ✅(244/244 × 3 全绿);Latency 实测 gate 待模型 receipt(登记为 C5/C6 前置)