# A1b Bulk Vision QA 报告（新补录会话全量层）

> 模型: deepseek/deepseek-v4-flash-vision-exp (reasoning off) ｜ 范围: 会话 20260912_090902 全部自动采样 JPG 829 张(含 snapshots) ｜ 17 批 ｜ 日期: 2026-09-12

## 1. 总体结论

- **场景覆盖完整**: combat 631 / shop_bench 96 / other 70 / loading 14 / **end 7** / lobby 7 / **carousel 4** —— 补全了旧数据缺失的选秀与结算场景。
- **后期棋局密度高**: u=16:152、u=18:155、u=15:84、u=14:20、u=13:5 —— 9 人口堡取样充足。
- **卫生**: border=none 829/829；黑帧 4 张（i=148,523,561,720）。
- 全量层 `e=true` 307 张 —— 经抽校层 1120px 复核为 0（分辨率伪影），最终由高分辨率裁剪核验证实（见 `docs/reviews/vision-qa/a1_gate_final.md`）。

## 2. 成本

- 17 批 ≈ $0.25；单批一次通过（reasoning 禁用方案稳定）。

## 3. 输出物

- `docs/qa_runs/a1b/bulk_results/batch_*.json`
- `docs/qa_runs/a1b/manifest.csv`