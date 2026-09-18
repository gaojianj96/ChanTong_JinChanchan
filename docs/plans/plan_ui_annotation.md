# UI 桌面窗口 + 边运行边标注 + 回顾帧金标 · 规划 v3(定稿)

> 阶段: 已通过需求 → 委员会审议 → Supreme Arbiter(Fable 5)终审(有条件批准)。用户已拍板全部终审修正。**已定稿, 可出实现契约。**
> 终审结论: 有条件批准, 修正 4 处硬伤 + 5 项裁定后无需再上委员会。

---

## 一、用户最终决策(累计 2026-09-16)

| # | 决策点 | 定案 |
|---|---|---|
| 1 | 帧捕获 | 500ms 一次(2fps); WgcCaptureOptions 改**可配帧间隔**(非新增硬编码档) |
| 2 | 修正触发决策 | 手动点击重算, 不自动 |
| 3 | 标注语义 | **回顾时未修正的格子 = 正确**(不必显式"确认正确"; 分母自然成立) |
| 4 | 阵容码复制 | 延后(P2) |
| 5 | 帧持久化 | 只保留**关键帧(= 数值变化帧)**, 非关键帧直接舍弃 |
| 6 | 修正锚定 | 锚定 **FrameId** + 进入修正态时**钉住当前帧** |
| 7 | 修正粘滞 | 修正以格粒度粘滞, 识别值实质变化才失效 |

---

## 二、需求全景

### 2.1 UI 独立桌面窗口
- Avalonia UI 11+ / SkiaSharp + CommunityToolkit.Mvvm(MVVM 克制用, 不引导航框架/不搞 DI 全家桶)
- 独立普通窗口; 未来手机端悬浮窗仅保证 UI/业务解耦, 不本期实现

### 2.2 实时窗口(对局中)
- 展示识别数据: 金币/等级/阶段/血量/棋盘28格/备战9/商店5(英雄名/星级/装备/来源层级)
- 手动修正: 点格 → 候选 → 写 CorrectionRecord(仅记录)
- 手动"重算"按钮
- 阵容推荐(复用 TacticalAdvisor)

### 2.3 回顾窗口(金标标注工位)
- 回放某帧 → 展示该帧识别到的**全部数据** + 原图 + 区域框叠加渲染
- 用户修正错的 → GoldLabel(修正类)
- **未修正的格子 → 默认"正确"**(计入准确率分母)
- 产出该帧完整金标

---

## 三、数据模型(吸收终审修正)

### 3.1 CorrectionRecord(实时仅记录, 锚定 FrameId)
```csharp
public sealed record CorrectionRecord(
    string Id,
    string MatchId,
    string FrameId,             // 帧指纹(修正锚定的目标帧)
    DateTimeOffset Timestamp,
    long SnapshotVersion,       // 仅用于重算竞态, 不用于修正提交
    string RegionType,          // 字段级: 棋盘.英雄 / 棋盘.星级 / 棋盘.装备 / 备战席.英雄 / ...
    int CellIndex,              // 格子索引(其它为 -1)
    string? RecognizedValue,    // null = 漏检
    string CorrectedValue,      // 结构化 JSON 子对象(英雄+星级+装备)
    double Confidence,
    string SourceTier,          // T0-T3
    string RecognizerVersion,   // 分区域识别器版本
    string SchemaVersion);      // "1"
```

### 3.2 GoldLabel(回顾二次确认后, CorrectionId 可空)
```csharp
public sealed record GoldLabel(
    string Id,
    string? CorrectionId,       // 可空: 无修正则 null, 代表"确认正确"
    string MatchId,
    string FrameId,
    long SnapshotVersion,
    string RegionType,          // 字段级(同 CorrectionRecord)
    int CellIndex,
    string? RecognizedValue,
    string CorrectedValue,      // null/"空"必须合法表达误检(该格实为空)
    double Confidence,
    string SourceTier,
    string RecognizerVersion,
    string? CorrectionType,     // 漏检/误检/定位错/分类错/确认正确
    string SchemaVersion,       // 补上(纠正倒挂)
    bool Revoked,               // 墓碑: 标注者也会标错, 支持撤销
    DateTimeOffset ConfirmedAt);
```

### 3.3 关键语义(吸收终审硬伤修复)
- **"未修正=正确"**: 回顾窗口完成一帧校验后, 未修正格自动生成 `CorrectionType="确认正确"` 的 GoldLabel, 使准确率分母完整。
- **FrameId 锚定**: 修正的目标是"用户看到的那一帧", 进入修正态时 UI 钉住当前帧(冻结), 提交后回直播流。
- **粘滞语义**: 修正以格粒度粘滞; 该格识别值发生实质变化(换英雄/升星/换装备)才失效; UI 标示"此格显示修正值"。
- **墓碑撤销**: append-only JSONL 下用 `Revoked` 记录作废错误金标。

---

## 四、存储(吸收终端"帧持久化"硬伤)

```
data/meta/... (既有无)
corrections/{matchId}/
├── corrections.jsonl      # CorrectionRecord(实时记录)
├── gold_labels.jsonl      # GoldLabel(回顾确认, 含墓碑撤销)
frames/{matchId}/
└── {frameId}.png          # 关键帧(仅数值变化帧落盘)
```

- 关键帧判定: 仅当 GameStateSnapshot 发生数值变化才持久化(捕获 2fps, 落盘 << 捕获率);
- 保留策略: N 局滚动 / 标注中手动锁定;
- FrameId = 帧内容哈希; 帧图需含布局几何元数据(分辨率/版本)供回顾叠框;
- MatchLogger 只写轻量 `CorrectionRecorded` 事件(含 correction id 指针, 不含 payload); **GoldLabel 绝不进事件流**(延迟标注破坏时序); payload 单一事实源在 corrections 目录。

---

## 五、数据流(终审定稿版)

```
捕获(2fps, 可配间隔) → 识别 → GameStateSnapshot + RecognitionFrame + 关键帧落盘
   → 实时 UI 显示
   → 用户修正(钉帧 + 仅记 CorrectionRecord + 粘滞)
   → 手动重算 → TacticalAdvisor → 刷新推荐
   → (回顾) 回放关键帧 + 原图叠框 → 修正/未修正都产出 GoldLabel → EVAL-RUNNER 测准确率
```

---

## 六、任务 DAG(终审拆分顺序)

| 批 | id | 任务 | 依赖 |
|---|---|---|---|
| 一(并行) | UI-MODEL | 数据模型 + JSONL 存储 + 墓碑撤销 | - |
| 一 | FRAME-STORE | 关键帧持久化(变化去重 + FrameId 哈希 + 保留策略) | - |
| 一 | CAPTURE-RATE | WgcCaptureOptions 改可配帧间隔 | - |
| 二 | UI-WINDOW | Avalonia 窗口骨架(MVVM 克制) | - |
| 三 | UI-LIVE | 实时显示 + 钉帧修正 + 粘滞 + 手动重算 | UI-WINDOW, UI-MODEL, FRAME-STORE |
| 四 | UI-REVIEW | 回顾: 帧回放 + 原图叠框 + 二次确认 + 默认正确 + FrameArchiveReader | UI-WINDOW, UI-MODEL, FRAME-STORE |
| 五 | EVAL-RUNNER | 金标评测执行器, 产出分区域/分层级/分版本准确率报告 | UI-MODEL, FRAME-STORE |
| 贯穿 | UI-INTEGRATE | 随批次持续接线, 非末尾大爆炸 | - |

**强制约束**:
- 帧/事件/快照读取封装为同一组件 `FrameArchiveReader`(回顾窗口与 R4 --replay 共用, 防分叉);
- EVAL-RUNNER 在 P0 范围内(否则"识别质量可证"立项理由不成立);
- CellIndex 坐标约定(棋盘28格行列映射/备战席方向)必须写进契约文档。

---

## 七、已明确不做/延后

- 阵容码复制(P2, 待 MetaInfo CompCode)
- M5 Overlay(用户已声明跳过)
- 手机端悬浮窗(仅保证解耦, 不实现)
- 自动重算(明确手动)