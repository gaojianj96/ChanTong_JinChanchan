# 仲裁记录: C1a(几何 Schema + 帧丢弃可观测)双盲评审

> Planner ｜ 2026-09-12 ｜ Kimi K3(通过) + Claude Sonnet 5(Pass-with-Notes)

## 裁定

| # | 意见 | 来源 | 裁定 | 处置 |
|---|---|---|---|---|
| 1 | Reader.Count>=Capacity 非原子,多写者下丢帧事件可能失真 | Sonnet P1 | Important(遥测级精度,不涉正确性) | ✅ 事件语义注释 + 记债 D-13;C3 遥测接入时精度复核 |
| 2 | 订阅者异常会中断帧处理循环 | Kimi Minor | Important | ✅ RaiseFrameDropped try/catch 包裹(双服务) |
| 3 | FromJson 缺 Row/Col/Index 唯一性与范围校验 | Sonnet P2-5 + Kimi Minor-3 | Important | ✅ 校验落地 + 2 个新测试(225/225) |
| 4 | 魔数/硬编码坐标表 | Kimi Major-1 | Rejected | 校准数据表即契约本身,来源由 Source 字段 + calibration_final.json 追溯 |
| 5 | ShouldAccept 双服务重复,应抽 FrameThrottler | Kimi Major-2 | Optional | 记债 D-14 |
| 6 | Mat 对象池(C3 范围) | Kimi Major-3 | Optional | 记债 D-15,绑定 C3 |
| 7 | ComputeStaggerX 无符号均值掩盖方向 | Sonnet P2-3 | Optional | 记债 D-16(信息指标) |
| 8 | Source 版本哈希绑定 | Sonnet P2-4 | Rejected | 过度工程,标注为不做 |

## 终局: C1a 合并 ✅(225/225 × 3 轮全绿)

修复后 summary: 双服务统一 RaiseFrameDropped;FromJson 结构化校验;新增 malformed/duplicate 测试。