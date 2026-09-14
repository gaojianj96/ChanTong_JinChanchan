# Milestone 4 Implementation Plan: [Engine] 局内全状态机、7 名对手侦察快照与超几何分布决策推演引擎

## 1. 任务背景与目标
- **目标 Issues**:
  - `Issue #13: [Engine] GameState 局内全状态机与 7 名对手侦察快照追踪`
  - `Issue #14: [Engine] 全局牌库时序追踪与超几何分布抽卡期望概率算法`
- **所属阶段**: `Milestone 4: 局内状态机、牌库时序与超几何分布推演`
- **核心目的**: 构建纯内存、毫秒级（$< 2\text{ms}$）响应的对局决策中枢，实现对 1-1 到决赛圈全生命周期状态的建模、7 名对手阵容/备战席实时快照缓存、全局各费卡剩余牌池追踪，以及基于超几何分布 (Hypergeometric Distribution) 精准推算下一抽/D牌命中概率与保姆级战术建议。

---

## 2. 详细技术实现方案

### 2.1 依赖引入与扩展 (`src/ChanSight.Core/`)
- 引入数学运算基础与不可变数据集合 `System.Collections.Immutable`

### 2.2 核心模型与算法设计 (`src/ChanSight.Core/Engine/` 或 `src/ChanSight.Core/Models/`)
1. **`GameStateSnapshot` (不可变全局对局快照)**:
   - `Stage` (如 "3-2"), `Gold`, `Level`, `Exp`, `Hp`, `Streak` (连胜/连败)
   - `BoardUnits` (28格弈子列表与星级装备)
   - `BenchUnits` (9格备战席弈子)
   - `CurrentShop` (5格商店卡牌)
   - `ActiveTraits` (激活的羁绊层级，如 "灵魂莲华": 5)
   - `Opponents` (7名对手的独立快照缓存：`OpponentSnapshot`)
2. **`HypergeometricEngine` (`IHypergeometricEngine`)**:
   - 官方各费用卡池总量参数：1费22张、2费20张、3费17张、4费10张、5费9张（可配置）；
   - 各等级（Level 1~10）商店各费用刷新概率表（Shop Odds）；
   - **超几何分布公式计算**:
     $$P(X \ge n) = \sum_{i=n}^{\min(n, M)} \frac{\binom{M}{i}\binom{N-K-M}{5-i}}{\binom{N-K}{5}}$$
   - `CalculateRollProbability(int targetCost, int currentLevel, int neededCopies, int currentOwned, int goldToSpend, int copiesTakenByOthers)`: 精确输出期望命中概率。
3. **`GameStateManager` (`IGameStateManager`)**:
   - 状态机流转：准备阶段 (Planning) $\rightarrow$ 战斗阶段 (Combat) $\rightarrow$ 选秀阶段 (Carousel) $\rightarrow$ 野怪阶段 (PvE)；
   - 对手侦察追踪：当检测到画面切入非己方棋盘时，自动将识别结果更新至对应对手槽位快照中；
   - 战术建议生成器 (`TacticalAdvisor`): 输出何时存利息、何时升人口、推荐保留棋子与装备合成路线。

### 2.3 单元测试设计 (`tests/ChanSight.Tests/Engine/`)
- `HypergeometricEngineTests.cs`: 验证概率单调性、极端边界（卡池抽空、0金币、必出）、理论数学期望值比对。
- `GameStateManagerTests.cs`: 验证状态机切换、7 名对手快照独立缓存与并发读取安全。
- `TacticalAdvisorTests.cs`: 验证 50 利息保护、升人口时机推荐与装备契合度推荐逻辑。

---

## 3. 验收标准
- `dotnet build` 0 错误 0 警告；
- 单元测试覆盖状态机流转、对手快照与超几何分布期望数学计算；
- 单次概率推演与战术建议耗时 $< 1\text{ms}$。
