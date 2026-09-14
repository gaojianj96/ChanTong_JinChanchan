# Batch Review B1/B2/B4/B5/C0b - Claude Sonnet 5

# Sprint B 余项 + C0b Full Review

## B1: VideoRecorderService.cs

**结论:通过,有轻微遗留问题**

- **[Minor]** `RecordFramesAsync` 中帧号 `frameCount` 是本地变量,但 `_framesWritten` 用 `Interlocked.Increment`——两者永远同步递增(单一写者协程),Interlocked 无实际并发意义,只是给 `IsRecording`/外部读线程提供可见性,这点合理,非缺陷。
- **[Minor]** `WriteMetaAsync` 中 `durationSeconds`/`frameCount` 均序列化为字符串或 0(provisional 时),而非 `null` 字面量——`endedAt = provisional ? null : ...`,C# 三元表达式两分支类型需一致,这里 `null` 与 `string` 合法(可空),但 `durationSeconds` 同理,无编译问题,构建通过验证了这点。
- **[Minor]** `CleanOrphanProvisionalMetaAsync` 仅把孤儿 meta 重命名为 `.orphan`,没有清理逻辑上作废该次 orphan 写入后是否会被二次覆盖(下次 StartAsync 写同名 `meta.json` 无冲突),逻辑正确。
- **[Info]** `StopAsync` 中 `_recordingTask` 异常被吞掉仅记警告,属既定策略(尽力落盘 meta),可接受。
- **[Info]** `DisposeAsync` 调用 `StopAsync()` 未传入外部 token,属默认收尾行为,合理。

整体实现清晰,状态机简单可靠,无阻断问题。

## B2: NativeHotKeyApi.cs

**结论:通过**

- **[Minor]** `RunMessageLoop` 中 `MsgWaitForMultipleObjectsEx` 返回值未检查(WAIT_FAILED 时应仍继续 PeekMessage,当前逻辑是无论如何都跑 PeekMessage 循环),影响仅是低效轮询而非功能错误,可接受。
- **[Minor]** `WndProcDelegate` 字段 `_keptAliveWndProc` 用于防止委托被 GC——正确防御措施,好实践。
- **[Info]** `DisposeAsync` 中 `_messageThread.Join` 超时后无日志/无强制退出,静默失败,建议后续加日志(非阻断)。
- **[Info]** 消息循环退出依赖 `cts.Token.IsCancellationRequested` 轮询而非 `PostMessage(WM_QUIT)` 主动唤醒,存在最多 100ms 退出延迟,可接受但非最优雅。

窗口类名用 GUID 保证唯一性,资源释放顺序正确(先反注册热键,再销毁窗口,再等线程退出)。无阻断缺陷。

## B4: DatasetSamplerService.cs

**结论:通过**

- **[Minor]** `TakeManualSnapshotAsync` 优先从 channel 抢帧,失败后 fallback 到 `_latestFrame`,存在竞态:`SampleFramesAsync` 也在更新/释放 `_latestFrame`,两者用 `_latestFrameLock` 互斥,逻辑正确无空竞争。
- **[Minor]** `SampleFramesAsync` 里对每帧都 `Clone()` 一次存入 `_latestFrame`(无论是否命中采样间隔),存在一定开销(每帧一次 clone+dispose),在高帧率下可能是性能热点,但功能正确,标记为性能优化建议而非缺陷。
- **[Info]** 写队列 `DropWrite` 策略配合 caller 侧 `clone.Dispose()` 处理,资源不泄漏,好实践。
- **[Info]** `StopAsync` 顺序:先停采样任务,再 complete 写队列,再等 writer,再写 index——顺序正确,不会漏样本。

无阻断问题。

## B5: WgcCaptureService.cs

**结论:通过**

- **[Minor]** `MaxFrameCallbackMilliseconds` 用无锁 CAS 循环更新,实现正确但仅用于 UI 展示指标,非关键路径。
- **[Minor]** `OnProviderCaptureEnded` 中先在锁内清空 `provider`/`IsRunning`,锁外触发事件+异步 dispose,顺序合理避免死锁,但 `CaptureEnded` 事件发出后 `IsRunning=false` 已生效,订阅者(InteractiveDashboard)据此判断状态一致。
- **[Info]** `StartAsync` 失败回滚时用 `ReferenceEquals(provider, nextProvider)` 判断避免误清已被替换的新 provider,细节严谨。

实现健壮,异常路径资源清理完整,无阻断问题。

## C0b: InteractiveDashboard.cs(Kimi 修复后)

**结论:通过,已解决此前并发/资源问题**

- **[Minor]** `ToggleRecordingAsync` 用 `Interlocked.Exchange(ref _toggleGuard,...)` 防抖,但 F6 热键路径 `OnHotKeyPressed` 未走该 guard,直接调用 `StartRecordingAsync/StopRecordingAsync`——若用户同时按键盘 R 与热键 F6,仍可能并发触发。**建议**统一走 `ToggleRecordingAsync` 保护路径,当前非阻断但存在极小竞态窗口。
- **[Minor]** `CreateStatusPanel` 中 `duration` 计算的 if/else 两分支逻辑完全相同(`_isRecording && _currentSession is not null` 与 `_currentSession is not null` 结果一致),属死代码冗余,无功能影响,建议简化。
- **[Info]** `OnHotKeyPressed`/`OnCaptureEnded` 为 `async void`,异常已在内部 try/catch 兜底,不会造成未处理异常崩溃,可接受。
- **[Info]** `ShutdownAsync` 释放顺序:停录制→停 capture→解绑事件→dispose 各服务,顺序合理,无遗漏。

## 整体意见

五个组件配合形成完整录制流水线,职责边界清晰,资源生命周期管理(尤其 IAsyncDisposable 链路)在本轮修复后表现扎实。测试证据(0 错,217/217×3 轮全绿)与代码审查结论一致,未发现阻断级(Blocker/Critical)问题。识别到的均为 Minor/Info 级别优化点(死代码、防抖覆盖不全、性能微优化),建议记入技术债 backlog,不阻塞本 Sprint 交付。**建议合并。**