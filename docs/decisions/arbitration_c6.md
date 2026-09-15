# 仲裁记录: C6(PaddleOCR 字典纠错层)双盲评审

> Planner ｜ 2026-09-12 ｜ Kimi(Approve)/ Sonnet(Pass with scope caveats)

| # | 意见 | 来源 | 裁定 | 处置 |
|---|---|---|---|---|
| 1 | 阈值边界拒绝/替代/零距离近失配测试缺 | 双审 | **Important** | ✅ 补 6 项(边界/替代/并列/空输入/Items 噪声) |
| 2 | 并列候选确定性未钉住 | 双审 | **Important** | ✅ 钉为"结果 ∈ 候选集"(HashSet 序不稳,不钉具体项) |
| 3 | TryParseNumeric 噪声语义未验证 | 双审 | **Important** | ✅ "3.5"→35 按实现语义钉住(去噪提取) |
| 4 | 大小写/空白规整未测试 | Kimi Minor | 记债 | D-22(词典匹配采用 OrdinalIgnoreCase,空白规整在 OCR 层) |
| 5 | 集合健康性(空串/重复/下限) | Kimi Minor | 记债 | D-23 |
| 6 | RecognizeTextFromRegion 是未验证接缝,需收据跟踪 | 双审 | 条件 | ✅ 登记 receipt 台账(与 C5 合并) |

## 终局: C6 合并 ✅(274/274 × 3 轮全绿)