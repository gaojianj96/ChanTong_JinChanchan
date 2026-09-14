# 仲裁记录: C3a(IFramePool + CapturedFrame 扩展槽)双盲评审

> Planner ｜ 2026-09-12 ｜ Kimi(C+)/ Sonnet(B+)

| # | 意见 | 来源 | 裁定 | 处置 |
|---|---|---|---|---|
| 1 | Reset "泄漏旧 Mat" | Kimi-1 | **Rejected(误报)** | 证据: Return→frame.Dispose→_image.Dispose 已释放;Reset 的 !wasDisposed 分支不重复释放 |
| 2 | 容量未强制,池无限增长 | Kimi-2 + Sonnet | **Blocker** | ✅ 硬上限:Rent 满池抛 InvalidOperationException;测试更新 |
| 3 | 池缺少 IDisposable(析构释放全部壳) | Sonnet P3 | **Important** | ✅ Dispose 实现 + 测试 |
| 4 | 非池成员 Return 静默忽略需文档 | Sonnet P1 | Minor | 已注释说明(调用方自释) |
| 5 | RentedCount O(n) | Sonnet P1 | Minor | 债务(池小) |
| 6 | 56B/cycle 依赖 OpenCvSharp 版本,flaky 风险 | Sonnet P2 | Minor | 注释固定说明 |
| 7 | Tag Reset 强制 null 语义 | Kimi | Minor | 设计如此(槽位复用语义),记录 |

## 终局: C3a 合并 ✅(250/250 × 3 轮全绿)