# 仲裁记录: C4.5(PostProcessor 抽象)双盲评审(重审版)

> Planner ｜ 2026-09-12 ｜ 首轮评审因编排脚本误嵌 C2 文件被判 Fail(流程事故,已纠正),重审通过

## 重审结论: Kimi 通过 / Sonnet Pass-with-Notes(无 Blocker)

| # | 意见 | 裁定 | 处置 |
|---|---|---|---|
| 1 | 选项未校验(MaxDetections<0、阈值区间) | Important | ✅ Process 入口三重校验 |
| 2 | IoU 整型溢出风险(超出 int 范围) | Important | ✅ 全 double 计算 |
| 3 | MaxDetections 截断语义需注释 | Minor | 债务 |
| 4 | NMS O(n²) 大候选瓶颈 | Minor | 债务(候选暂小) |
| 5 | ROI 中心判定边界文档/Rect? 用法 | Nit | 债务 |
| 6 | 严格 > 阈值 + 等式保留测试覆盖 | 已满足 | 240/240 含 IoUAtThreshold 用例 |

## 终局: C4.5 合并 ✅(240/240 × 3 轮全绿)