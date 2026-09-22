# 识别与推荐架构演进 · 设计定稿 v2

> 状态: 定稿候选(需求 + 委员会审议 + 用户决策已收敛)。**尚未派发实现。**
> 前置: 委员会已审议(见 `docs/discussions/recognition_manual_vlm_review_live.md`), 用户认可全部修正并补充信息模型。

---

## 一、核心方向(定稿)

1. **识别触发**: 自动 + 手动并存。**本轮手动整帧 VLM 为权威兜底**, 自动识别维持现状(已知不准, UI 标注"未校准"), 待后续真 OCR/校准/金标达标后恢复自动。
2. **手动整帧 VLM**: 一次识别整帧全部信息(阶段/血量/等级/金币/棋子/装备/备战席/对手序号/玩家名), 己方或对手(手动切屏)。
3. **2fps 录制保留**: 作为后续训练数据, 不废弃。
4. **双轨推荐**: 自动算法(数据更新后实时)+ 手动 LLM(手动触发), 并显标注「算法推荐」/「LLM 推荐」。
5. **手动改所有数据**: 可改阶段/血量/经验/金币/棋子名/星级/装备(自己与对手)。
6. **回放重模拟**: 用录制帧重放, 避免每次验证开新局; 识别直读已有结果, VLM 仅手动点帧触发。

---

## 二、统一 Schema 扩展(冻结, 本轮核心基础变更)

### 2.1 己方与对手的一致信息模型(用户定稿)

| 维度 | 自己 | 对手 |
|---|---|---|
| 玩家名 | ✅ | ✅(新增) |
| 阶段/血量/等级/经验/连胜 | ✅ | 血量/等级 nullable |
| 棋盘 28 格(名/星/装备) | ✅ | ✅ |
| 备战席 9 格(名/星/装备) | ✅ | ✅(能看见) |
| 备选装备 | ✅ | ✅(能看见) |
| 经济(金币) | `int Gold` 确定值 | `int GoldEstimate` 下限档位(0/10/20/30/40/50) |
| 商店 5 卡 | ✅ | ❌ |

### 2.2 类型扩展(冻结)

```csharp
// BoardUnitState 增加装备(自己 + 对手通用)
public sealed record BoardUnitState(
    int SlotIndex,
    string? Name,
    int Star,
    int CopyCount,
    IReadOnlyList<string> Items);   // 新增: 装备名列表(对齐 GameSeasonDictionary.Items)

// OpponentSnapshot 增加 玩家名 / 经济下限 / 备战席
public sealed record OpponentSnapshot(
    int PlayerIndex,
    string? PlayerName,            // 新增: 玩家名
    int? Hp,
    int? Level,
    int GoldEstimate,              // 新增: 经济下限档位 0/10/20/30/40/50(int)
    IReadOnlyList<BoardUnitState> BoardUnits,
    IReadOnlyList<BoardUnitState> BenchUnits,   // 新增: 对手备战席
    long Version);

// GameStateSnapshot 增加己方玩家名
public sealed record GameStateSnapshot(
    string Stage,
    GamePhase Phase,
    int Gold,
    int Level,
    int Exp,
    int Hp,
    int Streak,
    string? PlayerName,            // 新增: 己方玩家名
    IReadOnlyList<BoardUnitState> BoardUnits,
    IReadOnlyList<BoardUnitState> BenchUnits,
    IReadOnlyList<ShopCardState> ShopCards,
    IReadOnlyList<OpponentSnapshot> Opponents,
    long Version);
```

### 2.3 影响面
- `RecognitionPipeline` / `RecognitionToGameStateAdapter`: 识别产物需填装备、己方名、对手名/经济/备战。
- `TacticalAdvisor`: 读 GoldEstimate 时需知它是下限(算法用于估计时注意语义)。
- `OpponentScoutTracker`: 对手快照补玩家名/经济/备战席。
- `UI`(Live/Review): 显示装备、玩家名、对手经济档位。
- 现有测试: 涉及 `BoardUnitState`/`OpponentSnapshot`/`GameStateSnapshot` 构造的测试需同步新增字段(有默认值, 减少破坏)。

---

## 三、手动整帧 VLM 契约(委员会修正后)

### 3.1 输入(不做裸整帧 4K)
- 分区域裁剪后**多图输入**:
  1) 顶部 HUD 条(阶段/血量/等级)
  2) 对手信息条(玩家名/序号/经济/等级)
  3) 棋盘区域(28 格, 己方或对手)
  4) 备战席 + 备选装备区域
- 允许用户覆盖 ROI(未来)。

### 3.2 输出(严格 JSON, 一次性覆盖, 缺失用 null)
```json
{
  "perspective": "self" | "opponent",
  "opponentIndex": 0-6 | null,          // 对手序号; VLM 难直接读数字, 由"信息条位置+玩家名+等级"后端反查
  "playerName": "…",
  "stage": "3-2",
  "hp": 80, "level": 6, "exp": 10, "gold": 52,     // gold 对手时为下限档位
  "goldEstimate": 50,                              // 对手经济下限(0/10/20/30/40/50)
  "streak": 2,
  "board": [ {"slot":0, "hero":"盖伦", "star":2, "items":["羊刀"]} ... ],   // 28格
  "bench": [ {"slot":0, "hero":"拉克丝", "star":1, "items":[]} ... ],      // 9格
  "confidence": 0.9
}
```
- **来源标注**: 所有手动 VLM 结果写 `SourceTier = T2`(可加 `manual_vlm` 标记)。
- **低置信字段**: 强制 UI 标黄/标红待修; 禁止静默覆盖旧值。
- **对手序号**: 由"信息条位置 + 玩家名 + 等级"组合反查映射, VLM 不直读数字; 序号在 UI 显式供用户确认。

### 3.3 成本控制
- 手动触发频率受控(用户点才调);
- 回放中禁自动 VLM, 仅手动点帧触发;
- 结果缓存(frame_hash + prompt_version)。

---

## 四、双轨推荐 + 数据健康度

- **算法推荐**(TacticalAdvisor): 数据更新后去抖(~300ms)实时跑; 卡片标注「算法推荐」。
- **LLM 推荐**(LLMAdvisor): 手动触发; 标注「LLM 推荐」+ 时间戳 + 数据快照 ID; 缓存最近 3 条。
- **数据健康度**: 当数据含"自动未校准"标记(自动识别产物), 推荐卡片顶部提示 "⚠ 数据未校准"; 低于阈值时推荐置灰, 强制"先手动校准本帧"。
- 手动编辑数据后: 算法自动重跑, LLM 不自动跑。

---

## 五、回放重模拟

- 数据源: **FrameArchive 关键帧做时间轴骨架** + VideoRecorder/DatasetSampler 2fps 帧间插值。
- 链路: 帧 → 快照(直读, 按 frame_id 挂载)→ 战术状态 → 算法推荐(重算); **LLM 不重算**。
- VLM: 回放中禁自动; 用户右键单帧触发, 走同一手动 VLM 管线, 结果写回 AnnotationStore + 刷新该帧快照。
- 未识别帧显示"未识别"占位 + 视觉标记。

---

## 六、实现 DAG 初稿(定稿后拆分)

| id | 任务 | 前置 |
|---|---|---|
| SNAPSHOT-EXT | 统一 schema 扩展(BoardUnitState.Items / Opponent 名+经济+备战席 / 己方名) | - |
| VLM-FRAME | 手动整帧 VLM 服务(裁剪多图 + JSON 契约 + 校验 + 来源标注) | SNAPSHOT-EXT |
| MANUAL-UX | UI 手动触发 + 对手序号确认 + 字段级修正 + 数据健康度标记 | SNAPSHOT-EXT, VLM-FRAME |
| DUAL-RECO | 双轨推荐(算法实时 + LLM 手动 + 来源标签 + 健康度置灰) | SNAPSHOT-EXT |
| REPLAY-SIM | 回放重模拟(FrameArchive 骨架 + 手动 VLM 触发) | SNAPSHOT-EXT, VLM-FRAME |

---

## 七、待用户最终确认

1. 上述 schema 扩展(Items/PlayerName/GoldEstimate/BenchUnits)是否即为最终冻结版;
2. 手动 VLM 输出契约(多图裁剪 + JSON + 对手序号反查)是否认可;
3. DAG 顺序与任务划分是否认可(认可后出实现契约)。

> 注: 本阶段仅设计, 未派发任何 worker。