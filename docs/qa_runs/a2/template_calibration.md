# A2 校准终版: 社区模板 + 视觉验证 (Web-Mined Golden Geometry)

> 日期: 2026-09-12 ｜ 方法: 联网检索(GitHub) → 模板提取 → 实机帧叠加验证(视觉核验员) → 校准定稿
> 用户提示: "我觉得你可以自己网上搜索"

## 1. 来源与许可

| 资源 | 内容 | 许可证 |
|---|---|---|
| `github.com/how-do-youeven/Computer-Vision-for-TeamFight-Tactics` `data/friendly_boxes.json` | 4×7 棋盘格 + 9 备战席归一化框 | **无 LICENSE(仅作参考引用,不并入分发代码)** |
| 同仓库 `core/slot_map.py` | PC 端 TFT 棋盘/备战席归一化中心点 | 同上 |
| 本项目既有 canonical | 商店 5 槽坐标 | 自有 |

## 2. 验证方法(全自动,无人工打点)

1. 三模板(红=既有 canonical、绿=friendly、蓝=slot_map)叠加到 6 张实机帧 → 视觉核验员粗判;
2. 棋盘区域放大裁剪(仅绿模板,4 帧)→ 精细评分:
   - 棋盘对齐分数 7/8/9/8;残差 < 4px @3840x2088(行方向 dy 0.5~2px@缩放,列方向 0.5~1px);
   - 行错位(stagger)基本正确(1 帧建议微减);
3. 商店区域放大验证: 既有 canonical 5 槽 score 8(dx+3 dy+2 px@缩放)。

## 3. 定性结论(重要,推翻旧假设)

- **我们的旧 canonical `BoardArea/BenchBounds/HexRowPitchY` 是错的**(红色模板评分 3~8 显著劣于绿色);
- 真实金铲铲布局(自上而下): 棋盘(4×7, 归一化 y≈0.38-0.65) → 备战席 9 格(y≈0.69-0.76) → 商店 5 卡(y≈0.80-0.85);
- 旧表中列距 130px 碰巧正确;行距应为 ~82px(旧值 145px 高估 77%);行错位 ~48px(旧值 65px)。

## 4. 交付物

- `calibration_final.json`: 校准终版(28+9+5 归一化 + canonical 坐标,含出处与验证记录)
- `annotations/core_{idx}.json` × 25: 核心候选帧预标注(board+bench+shop 全部坐标,status=prefill)
- `overlays/`、`zoomed/`: 验证证据图 + `zoom_verdict.json`、`overlay_verdict.json`、`zoom_shop` 判定
- 模板源: `templates/community_grid_templates.json`

## 5. 流程影响

- **A2 由"人工从零标注 1050 点"退化为"模板预填充 + 抽查"(人工工作 ≤ 10 分钟)**;
- 新增 Executor 任务 **C0**: 将验证后的 board/bench 几何写入 `ChanSight.Vision.Models.CanonicalRoiDefinitions`(修正旧值),作为 C1a/C2 的前置;
- 视觉核验员 SOP 更新: 细粒度几何校验必须用放大区域 + 单一模板逐个评,全图三模板粗判仅作淘汰。