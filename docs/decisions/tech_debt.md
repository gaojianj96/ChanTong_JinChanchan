# 技术债务台账 (Tech Debt Log)

> 维护: Project Planner ｜ 依据: 双盲评审分级中非阻塞项的归档处

| ID | 来源 | 描述 | 绑定计划 | 状态 |
|---|---|---|---|---|
| D-01 | B3/Sonnet P2 | DXGI CaptureLoop catch(Exception) 粒度过粗,瞬时错误与设备丢失未区分(可加 DetectDeviceRemoved 分流) | Sprint B 收尾 review | 待办 |
| D-02 | B3/Kimi P2-3a + Sonnet P2 | DxgiCaptureService 无单元测试(需 duplication/loop DI 接缝) | C3(DI 改造) | 待办 |
| D-03 | B3/Kimi P2-1 | DXGI StopAsync 取消(OCE)即跳过 ReleaseDxgiResources;WGC provider Stop 先 ThrowIfCancellationRequested | - | 待办(低概率路径) |
| D-04 | B3/Kimi P2-4 | DXGI Start 在锁内同步 D3D11CreateDevice/DuplicateOutput,长阻塞期间 Stop 被挡 | - | 记录取舍,不改 |
| D-05 | B3exec | v4.1-flash 在"全文重写"式任务中易架构幻觉(杜撰 Abstractions/IFrameProvider);经验: 修任务用"N 处锚点式最小补丁"格式 | 全部后续 Executor 契约 | 已沉淀为契约规范 |
| D-06 | B1 test run | `DatasetSamplerServiceTests.TakeManualSnapshotAsync_SavesToSnapshotsDirectory` 全量跑偶发失败、单跑通过(疑似固定目录残留) | B5(采样任务)时根治 | **已闭环**(B5: 手动快照增加 200ms 有界等待,5 轮×214 全绿) |
| D-07 | StageGate MiniMax P2 | 热键 ID 单调仅进程内,跨重启多实例未验证 | 后置 | 待办 |
| D-08 | StageGate MiniMax P2 | 回调耗时微统计未接遥测("测了没看") | 后置 | 待办 |
| D-09 | StageGate MiniMax P2 | B3 Closed/DeviceRemoved 订阅去重逻辑归档 architecture map | 架构地图文档 | 待办 |
| D-10 | 双盲批量评审 | NativeHotKeyApi: HWND 双毁竞态(低危)、Join(3s)超时静默、Register 初始化失败抛异常与 bool 契约不符 | 后置 | 待办 |
| D-11 | 双盲批量评审 | Dashboard dead code: duration 计算 if/else 两分支相同 | 后置 | 待办 |
| D-12 | 双盲批量评审 | Kimi 误报两起(手动快照"泄漏"、_latestFrame "双重释放")已驳回留档,证据: 逐行核对 + 锁语义 | 审计参考 | 已闭环 |
| D-13 | C1a/Sonnet P1 | Reader.Count>=Capacity 非原子判定: 多写者下 ChannelFull 遥测可能欠计(不影响队列正确性) | C3 遥测 | 待办 |
| D-14 | C1a/Kimi | ShouldAccept + 丢帧逻辑在 WGC/DXGI 双份,可抽 FrameThrottler | 后置 | 待办 |
| D-15 | C1a/Kimi | Staging Texture/Mat 对象池 | C3(IFramePool) | 待办 |
| D-16 | C1a/Sonnet P2-3 | ComputeStaggerX 无符号均值掩盖错位方向 | 后置 | 待办 |
| D-24 | A3 smoke | bench +3px@原生 下偏 + 棋盘水平跨度系统偏差 | 用户 R4 目检 | **已闭环**(两次反推修正: bench 右锚 pitch 117→103, board 右锚跨度比 0.8757;用户确认全对齐) |
| D-29 | R2 讨论 | CPU/DML 自动选优(probe 一次选快者)与跑分波动消噪 | 用户裁量 | **已实现**(InferenceDeviceType.Auto + ChoosePreferredDevice,warmup=5;LoadModel/Probe 自动选路);注意: 选优对机器负载敏感(训练并发时两路皆慢),建议 receipt 重测时静载 |
| D-30 | R2 讨论 | 动态形状导出(dynamic=True)以支持 <640 输入降级 | R5 训练会话产出 | 进行中(训练任务已含 dynamic=True 导出) |