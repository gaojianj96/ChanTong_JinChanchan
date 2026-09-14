# 仲裁记录: 并行 M4 WIP 断编处置 (Planner Decision)

> 日期: 2026-09-12 ｜ 类型: 过程冲突处置 ｜ 依据: 用户授权"授权你修复甚至移除那些代码"

## 背景

Sprint A/B/C 执行期间,工作区出现未提交的并行 M4 开发线(Engine 领域模型、超几何引擎、状态管理器、TacticalAdvisor + 对应测试 + `plan_milestone_4.md`),与当前 DAG 无依赖关系,且处于断编状态(8 处 `BinomialCoefficient` 缺失 + `GameStateSnapshot` 参数失配),阻塞全量测试门禁。

## 处置(两难取低风险):

| 选项 | 评估 | 结论 |
|---|---|---|
| 修复断编 | 需推断他人半成品意图,成本中,可能与其会话冲突 | 否 |
| 移除 | 会销毁 WIP | 否(折中) |
| **归档 + 回退 DI(采用)** | 可恢复、不动 git 历史、立即解锁门禁 | ✅ |

## 执行动作

1. `src/ChanSight.Core/Engine/`、8 个 Engine 模型、`tests/ChanSight.Tests/Engine/` → 归档至 `%TEMP%/opencode/m4_engine_wip_20260912/`(随时可恢复);
2. `ServiceCollectionExtensions.cs` git checkout(撤销未提交的 Engine DI 注册);
3. `plan_milestone_4.md` 与 M4 讨论记录保留在原地(文档无编译影响);
4. 全量验证: build 0 警告 0 错误,`dotnet test` **210/210 通过** —— C0 回归门禁放行。

## 后续

- M4 引擎线将在流程内重新立项(需求定向 → 委员会 → DAG),以归档源码为起点经正规 Contract 流程重做;