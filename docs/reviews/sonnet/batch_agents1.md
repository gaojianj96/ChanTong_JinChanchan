# Batch Review R0/E4/V0/D02 - Claude Sonnet 5

# 批量独立 Full Review

## R0 — MatchLogger
**结论: Pass-with-Notes**

1. [高] `FlushGameAsync` 每次读取全文件再拼接重写(`existing + Pending`),append-only 场景下 O(n²) 写入放大,大文件会严重退化;应改为真正的文件追加写(`FileStream` Append 模式)。
2. [高] `AppendAsync` 幂等判断 `e.Seq <= log.MaxSeq` 依赖单调递增序号,但 `GetOrLoadAsync` 仅扫描已落盘内容得到 `MaxSeq`,若进程重启前 `Pending` 未 flush 即崩溃,重启后会重复写入已丢失的中间事件(断点续写存在丢失风险,非“真幂等”)。
3. [中] `SerializerOptions` 为静态可变字段,虽当前未修改,但作为 `static readonly` 暴露风险,建议 `private static readonly` 的同时避免后续误改导致序列化不一致。
4. [低] 无最大 flush 时间/定时 flush 机制,仅按字节阈值触发,长时间无新事件时数据滞留内存无法落盘。

## E4 — CompKnowledgeBase
**结论: Pass**

1. [低] `Load` 抛异常而非返回错误集合,与 V0 的“返回错误清单”风格不一致,调用方需 try/catch,建议评估是否应统一为 Result 模式。
2. [低] `QueryByUnits` 未提供并列命中数的稳定二级排序(如按 Id),多个模板同分时顺序依赖 `List` 稳定排序,存在潜在不确定性风险(实际 LINQ `OrderByDescending` 稳定,但未显式声明契约)。
3. [低] 仅支持 `TopDirectoryOnly`,子目录模板会被忽略,未来扩展需注意。

## V0 — RecognitionFrame / Validator
**结论: Pass**

1. [低] 文档中字段名 `xp` 与代码 `exp` 不一致(章节3表格写 `xp`,章节6与代码为 `exp`),建议统一文档措辞避免后续理解歧义。
2. [低] Validator 未校验 `timestamp` 是否为合法 ISO 8601 格式,契约声明为 ISO 8601 但代码仅有空值检查缺失(实际未校验非空/格式),建议补充。
3. [低] `stage` 仅校验非空,未按契约要求校验 `"d-d"` 格式,存在契约与实现落差。

## D02 — DxgiCaptureService
**结论: Pass-with-Notes**

1. [高] `ComputeFailureFinalize`/`GetCropRect`/`ShouldAcceptFrame` 已抽为 `internal static` 纯函数便于单测,是好的可测接缝设计,但需确认对应单测文件是否已覆盖 AccessLost/ChannelFull/Throttled 三类丢帧路径及 finalize 竞态,评审材料未附测试文件无法确认覆盖率。
2. [中] `CaptureLoopAsync` 中 `catch (Exception)` 吞掉所有异常仅置 `failed=true`,未记录异常信息(日志/遥测缺失),故障定位困难。
3. [中] `RaiseFrameDropped` 中 `catch { }` 空catch 吞异常无日志,不利调试。
4. [低] `Contains` 静态方法用 `<=`/`<` 边界判断只检测左上角点在输出区域内,未验证右下角,极端多屏配置下可能选错输出。

## E0 — 遗产盘点报告
**结论: Pass**

1. [低] 报告结论详实、评级/止损分类清晰,但“可复用”项(如 `GameStateSnapshot`)缺少具体改造工作量/优先级排序,建议后续任务拆解时补充。
2. [低] 部分错别字/用词(如“协会单调递增”应为“协议/维护”,“硬件 阈值”应为“硬编码阈值”),不影响结论但建议校对。
3. [低] 数学坑清单详尽,但未标注各坑对应 E1 任务的验收断言编号,后续追溯稍有不便。

---
**总体**: 5 项任务中 3 Pass、2 Pass-with-Notes,无 Fail。核心风险集中在 R0 的追加写性能与幂等边界、D02 异常吞没缺日志,建议在合并前补齐单测证据链。