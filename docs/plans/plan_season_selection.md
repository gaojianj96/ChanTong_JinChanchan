# 赛季/模式选择 + 字典自学习 · 设计 v2(定稿候选)

> 状态: 需求澄清 + 委员会审议完成, 用户已拍板关键分层。定稿候选, 待最终确认后出 DAG。**尚未派发实现。**

---

## 〇、关键分层决策(用户拍板 2026-09-19)

| 维度 | 隔离粒度 |
|---|---|
| **字典(英雄/装备/羁绊)** | 按 **season** 隔离(S18/S16.5), mode 共用 |
| **VLM prompt** | 按 **season + mode** 定制(恭喜发财的识别提示与匹配不同) |
| **推荐算法** | 按 **season + mode** 定制(恭喜发财玩法规则不同) |
| **MetaInfo 阵容** | 按 **patch** 隔离(已有), season→patch 映射 |

---

## 一、需求(用户定稿)

1. 实时识别界面加"赛季+模式"选择(S18+匹配/狂暴, S16.5+恭喜发财)。
2. 字典按 season 隔离; mode 影响 prompt 与算法, 不影响字典。
3. 自学习(a+c): 识别到字典外名字记候候选, 用户回顾确认后正式收录(校准遗漏, 非从零)。
4. 初始字典三重来源: 自学习累积 / 一图流导入 / 网络搜索。
5. 每赛季独立 MetaInfo。

---

## 二、委员会审议吸收(3 处修正)

1. **Provider 拆读写两接口 + 不可变快照**: `ISeasonDictionaryReader.Get(season)→SeasonDictionary` / `ISeasonDictionaryWriter.AddCandidate/Confirm/Revoke`。不暴露可写集合; 灰度期旧 static 与新 provider 双读 + 日志 diff。
2. **确认三选一事务**: 用户确认时 = 映射已有(写别名/纠错, 不新增)/ 新增(写正式字典)/ 忽略。纠错与收录是不同事务。
3. **防脏**: 收录需 ≥2 次独立识别 或 用户显式"这是新条目"; 网络搜索仅 offline 生成候选包(含 source/url/time/conf), 人审后合并, 不实时进识别。

---

## 三、数据模型

```csharp
public sealed record SeasonModeKey(string SeasonId, string Mode);

// 字典按 season 隔离(mode 无关)
public sealed record SeasonDictionary(
    string SeasonId,
    IReadOnlySet<string> Heroes,
    IReadOnlySet<string> Items,
    IReadOnlySet<string> Traits);

// 读写分离
public interface ISeasonDictionaryReader {
    SeasonDictionary Get(string seasonId);
    IReadOnlySet<string> Heroes => Get(CurrentSeason).Heroes;  // (便捷, 经 SeasonContext)
}
public interface ISeasonDictionaryWriter {
    void AddCandidate(string seasonId, string entity, CandidateMeta meta);  // 对局中发现, 不入正式
    void Confirm(string seasonId, string entity, ConfirmKind kind);        // 回顾确认: Map/New/Ignore
    void Revoke(string seasonId, string entity);
}
public enum ConfirmKind { MapExisting, New, Ignore }
```

- 纯算法(`TryFuzzyMatch`/`ComputeLevenshteinDistance`/`TryParseNumeric`)保持 static, 但字典作为参数传入。
- `SeasonContext { SeasonId, Mode }` 作为识别/推荐管线的显式上下文, 沿管线透传(不全局可变)。

---

## 四、season→patch 映射与 Mode 影响面

| 配置 | 隔离 |
|---|---|
| 字典文件 | `data/season/{seasonId}/dictionary.json`(mode 无关) |
| VLM prompt 模板 | 每 `season+mode` 一份, 或运行时按 mode 参数化 |
| 推荐算法参数 | TacticalAdvisorOptions 按 `season+mode` 配置 |
| MetaInfo | `data/meta/{patch}/`(`season→patch` 映射表挂 SeasonRegistry) |

---

## 五、自学习闭环(统一金标)

```
对局中: 识别到字典外名字 → Pending(season 维度, 不入正式字典, 不进 prompt 候选)
回顾时: 用户确认 → 三选一事务:
   MapExisting  → 写别名/纠错(不新增)
   New          → 写入正式字典 + 关联金标(frame+box 溯源)
   Ignore       → 丢弃
```

---

## 六、待用户最终确认

1. 上述分层(字典按 season, prompt/算法按 season+mode)是否符合最终意图;
2. "确认三选一"(MapExisting/New/Ignore) 与防脏机制(≥2 次 或 显式声明)是否认可;
3. 认可后出实现 DAG(预估: SEASON-CORE 字典 provider / DICT-SELFLEARN 自学习 / PROMPT-ALGO-MODE prompt+算法按mode / SEASON-UI 选择器 + 网络搜索 seeder)。