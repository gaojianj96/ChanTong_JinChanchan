# 契约 V0：识别字段分级与精度契约 + JSON Schema

> Tech Lead 执行代理 ｜ 日期: 2026-09-15 ｜ 上游: scripts/tasks/V0_contract.md
> 定位: V1-V5 识别管线的输入契约,落地为可反序列化/校验的 C# schema 记录。

## 1. 目标

冻结识别帧中每个字段的"确定性 vs 语义"分级,明确每个字段由谁负责(本地 CV / VLM)、精度与取值范围,并落地为
`RecognitionFrame` record + `RecognitionFrameSchemaValidator` 校验器,供 V1-V5 管线作为稳定输入契约。

## 2. 字段分级总表

| 分级 | 责任方 | 特征 | 精度/来源 |
| --- | --- | --- | --- |
| **确定性字段 (Deterministic)** | 本地 CV | 由几何/OCR/模板匹配确定,可校验范围与精度 | 硬断言 |
| **语义字段 (Semantic)** | VLM | 需要候选名 + 置信度,无法仅靠 CV 唯一确定 | 软候选 |
| **元数据字段 (Metadata)** | 审计层 | sourceTier 层级 T0..T3、confidence、correctionFlag、timestamp | 审计追踪 |

## 3. 确定性字段 (本地 CV 负责)

| 字段 | JSON 类型 | 精度 / 范围 | 说明 |
| --- | --- | --- | --- |
| `gold` | int | units±0 | 金币,整数,不可有小数 |
| `level` | int | 0..9 | 玩家等级 |
| `stage` | string | 格式 `"d-d"`(如 `"3-2"`)| 阶段回合约 |
| `hp` | int | >=0 | 血量 |
| `exp` | int | ≥0 | 经验 |
| `star`(单元/卡) | int | 0..3 | 星级 |
| 槽位空/满 | bool | `true`=满 | 槽位是否被占用 |
| 装备图鉴 | `{iconId,count,iou}` | 每图标 iou>阈值 | 图标 id + 数量,匹配时 iou 必须超过阈值 |

## 4. 语义字段 (VLM 负责)

| 字段 | JSON 类型 | 结构 | 说明 |
| --- | --- | --- | --- |
| 弈子名 | `candidates[]` | `{name,confidence}` | 候选列表 + 置信度,非唯一 |
| 商店卡语义名 | `name` + `cost` | string + int | 商店卡片的语义名与费用 |
| 装备名 / 羁绊 | string | — | VLM 语义识别 |
| 海克斯名 / 描述 | string | — | VLM 语义识别 |

## 5. 元数据字段 (审计)

| 字段 | JSON 类型 | 说明 |
| --- | --- | --- |
| `sourceTier` 层级 | enum | T0..T3(来源可信层级) |
| `confidence` | double | [0,1] |
| `correctionFlag` | bool | 是否人工修正 |
| `timestamp` | string (ISO 8601) | 观测时间 |

## 6. Schema 结构 (RecognitionFrame)

C# 承载结构 `RecognitionFrame`(System.Text.Json)。JSON 字段名与下表一一对应,校验器按此断言。

| JSON 路径 | 类型 | 固定数量 | 取值范围 |
| --- | --- | --- | --- |
| `gold` | int | — | units±0 |
| `level` | int | — | 0..9 |
| `stage` | string | — | 非空 |
| `hp` | int | — | ≥0 |
| `exp` | int | — | ≥0 |
| `boardCells` | array | **28** | 每个 `{name?, star, items[], confidence, sourceTier}` |
| `benchCells` | array | **9** | 每个 `{name?, star, items[], confidence, sourceTier}` |
| `shopCards` | array | **5** | 每个 `{name, cost, confidence, sourceTier}` |
| `correctionFlag` | bool | — | — |
| `timestamp` | string | — | ISO 8601 |

- `star` ∈ [0,3]；`confidence` ∈ [0,1]；`sourceTier` ∈ {T0,T1,T2,T3}。
- 单元 (board/bench) 的 `name` 可为 null 当槽位为空；占位单元仍需保持数量与结构一致。
- 校验器在验证失败时返回错误清单 (非抛异常),便于测试与管线反馈。

## 7. 约束

- 不改动概率/状态/识别以外的内容;Vision 已有 `BoardGeometry` / `CanonicalRoiDefinitions` 保持不可动(仅读取,不修改)。
- 不引入新依赖,仅使用 System.Text.Json。

## 8. 验收

- `dotnet build` 0 错。
- `dotnet test --filter RecognitionFrame` 全绿。
- 文档与 schema 字段名一致(至少 board/bench/shop 字段名对应)。