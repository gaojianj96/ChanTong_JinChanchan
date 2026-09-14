# 仲裁记录: C2(Anchor 运行时校准)双盲评审

> Planner ｜ 2026-09-12 ｜ Kimi K3 + Claude Sonnet 5 双盲(均 Pass-with-Notes)

## 裁定与处置

| # | 意见 | 来源 | 裁定 | 处置 |
|---|---|---|---|---|
| 1 | Confidence 语义倒挂(Invalid 高置信压制 Valid 回退) | Kimi P1 | **Blocker** | ✅ Invalid 强制 Confidence=0 |
| 2 | 自洽但荒谬锚点(scale 100×)仍判 Valid | Kimi P2-2 | **Important** | ✅ 拟合 scale 与帧比例偏差 >50% 判 Invalid |
| 3 | 退化阈值绝对 ε 未随量纲缩放 | Kimi P2-3 / Sonnet Major-2 | **Important** | ✅ 相对阈值 ε·(n·Σx²+1) |
| 4 | 负 Offset 回归缺失 | Kimi P2-4 | **Important** | ✅ 负偏移锚点测试(232/232) |
| 5 | MaxReprojectionErrorPx 回退路径 0.0 与 NaN 不一致 | Sonnet Minor-5 | **Minor** | ✅ 统一 NaN |
| 6 | 独立一维回归非"联合 4 参 LSQ"命名误导 | Sonnet Major-1 | **Minor** | ✅ 注释澄清(不建模旋转,设计如此) |
| 7 | Confidence 系数 5 无出处 | Sonnet Major-3 | **Minor** | ✅ 注释标注经验系数 |
| 8 | AnchorPoints 语义(observed) | Sonnet Minor-4 / Kimi P3 | **Minor** | ✅ 改名 ObservedAnchorPoints |
| 9 | 魔数 20 | Sonnet Minor-6 | **Minor** | ✅ 具名常量 MaxPlausibleScale |
| 10 | 阶段门槛多显/混合 DPI 实证委托 B3,需交叉链接 | Kimi P2-4b / Sonnet ℹ️ | 记录 | B3 验收文档补交叉引用(见 arbitration_b3.md 更新) |

## 终局: C2 合并 ✅(232/232 × 3 轮全绿)