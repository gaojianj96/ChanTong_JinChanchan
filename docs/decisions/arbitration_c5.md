# 仲裁记录: C5(检测后处理真实化)双盲评审

> Planner ｜ 2026-09-12 ｜ Kimi / Sonnet 双盲(均 Pass-with-issues)

| # | 意见 | 来源 | 裁定 | 处置 |
|---|---|---|---|---|
| 1 | CHW 张量沿用 BGR 序,检测器需 RGB | Kimi P0-2 | **Blocker** | ✅ BGR2RGB 转换 + 通道交换回归测试 |
| 2 | 输出布局硬编码(仅 [1,stride,cells] ) | Kimi P0-1 + Sonnet P1-1 | 记债 | 标准 [1,84,8400] 匹配;变体交由实机 receipt 复核(D-19) |
| 3 | DetectedUnit 占位(name/star/cost 空) | Kimi Major-3 + Sonnet P1-2 | 记债 | 真实赛季分类映射留 C5.5/权重 receipt(D-20) |
| 4 | NMS 用 ClassicPostProcessor 而非旧 NonMaximumSuppression | Kimi Major-3b | 澄清 | 已是 Class-wise NMS(C4.5),旧方法保留但 Detect 未用 |
| 5 | RestoreBox int 截断/Lixiu etc | Sonnet P2 | 记债 | D-21 |

## 终局: C5 合并 ✅(262/262 × 3 轮全绿)