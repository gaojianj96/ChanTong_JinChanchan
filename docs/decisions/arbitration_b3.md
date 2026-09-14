# 仲裁记录: B3 (WGC/DXGI 资源生命周期) 终审

> Project Planner ｜ 2026-09-12 ｜ 评审输入: Kimi K3 + Claude Sonnet 5 双盲

## 评审结论综述

双审均 **Pass-with-Notes**。分级裁定:

| 问题 | 来源 | 裁定 | 处置 |
|---|---|---|---|
| DropOldest 驱逐帧 Mat 泄漏(双服务) | Kimi P1-1 | **Blocker** | 改 DropWrite + 调用方 Dispose;测试改断言 1,2 ✅ 已修 |
| DXGI FinalizeCaptureFailure 锁外释放竞态 | Sonnet P1 | **Blocker** | ReleaseDxgiResources 移入锁 + stale CTS 快照释放 + CancelAsync 容错 ✅ 已修(注: Sonnet 抓到我管线正则补丁静默失败的残留,价值极高) |
| 失败路径 CTS 泄漏/fire-and-forget | Kimi P1-2 | **Important** | staleCancellation 释放 ✅ 已修 |
| FrameReady 无订阅者帧泄漏 | Kimi P2-2 | **Important** | null-handler 防泄漏 ✅ 已修 |
| WaitForAsync 假阳性 | Kimi P2-3b | **Important** | 加最终断言 ✅ 已修 |
| 宽泛 catch 将瞬时错误误判为设备丢失 | Sonnet P2 + Kimi P2-1 | Optional | 记技术债务 |
| DXGI 服务无单元测试(需 DI 接缝) | Kimi P2-3a + Sonnet P2 | Optional | 记技术债务,绑定 C3 时补 |
| 取消即跳过释放路径 | Kimi P2-1 | Optional | 记技术债务 |
| Start 锁内同步创建设备 | Kimi P2-4 | Optional | 记技术债务(记录取舍) |
| OnItemClosed 复用于 DeviceRemoved 命名不直观 | Sonnet P3 | Rejected(不改) | 语义可读,避免过度抽象 |

## 终审判定: **B3 通过,合并**(修复后 build 0 错误,212/212 测试通过)

流程数据: Executor 主轮 1 + Fix Task 3 轮(其中 1 轮架构幻觉被驳回) + 双盲评审 1 轮(Kimi 首轮 reasoning-on 修复后重跑)。