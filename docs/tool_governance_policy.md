# 工具调用与执行权限安全策略规范 (Tool Invocation & Security Policy)

## 1. 目标与范围
本规范约束工作区中所有 AI 模型代理（Project Planner、需求与设计委员会、Tech Lead、Executor、独立评审组、Supreme Arbiter）在进行工具调用（Tool Calling / Function Calling）时的行为与权限范围，确保严格符合项目铁律（见 `README.md` §2 与 `docs/team_roles_and_workflow.md`）。

---

## 2. 工具调用权限矩阵

| 代理/模型角色 | 允许调用的工具类别 | 允许操作的目标路径 | 严格禁止的行为 |
| :--- | :--- | :--- | :--- |
| **Project Planner**<br>(DeepSeek V4 Pro 0813) | 读/搜/文档写/调度工具 | `docs/**`、根目录 `*.md` | 直接创建/修改源代码文件，发起未授权的破坏性命令 |
| **需求与设计委员会**<br>· DeepSeek V4.1 Flash<br>· MiniMax M3<br>· Qwen 3.8 Flash | 仅只读工具与讨论文档编辑工具 | `docs/discussions/**`、`*.md` | 任何源码写入/修改；契约、评审与裁决文档写入；终端破坏性命令 |
| **Tech Lead**<br>(DeepSeek V4 Pro 0813 #2) | 只读工具 + 契约/Fix Task/内部评审文档编辑工具 | `docs/contracts/**`、内部评审档案、`*.md` | 任何源码修改 |
| **Executor / Coder**<br>(DeepSeek V4.1 Flash) | 完整的代码读写与执行测试工具 | 全工作区（需遵循授权流程） | 在未取得用户明确授权前直接写入/覆盖除文档与 Contract 之外的代码文件 |
| **独立评审组**<br>· Moonshot Kimi K3<br>· Claude Sonnet 5 | 只读工具与各自独立的 Review 文档编辑工具 | 各自专属评审档案（如 `docs/reviews/kimi/**`、`docs/reviews/sonnet/**`），互不可见 | 任何源码修改；读写对方评审档案；互相交换评审意见 |
| **视觉核验员**<br>· 全量: DeepSeek V4 Flash Vision Exp<br>· 抽校: Qwen3-VL-30B-A3B-Thinking | 只读图像/文档工具 + 专属 QA 报告编辑工具 | `docs/reviews/vision-qa/**` | 修改源码或任何其他工件；越界写入；参与代码流程循环 |
| **Supreme Arbiter**<br>(Claude Opus 5) | 只读工具、裁决记录工具、授权代判（5 分钟超时且非安全/非成本事项） | `docs/decisions/**`、`*.md` | 直接修改源码；参与普通任务的规划/执行/评审；对安全与成本事项代判授权 |
| **用户** | 全部（人工操作） | 全工作区 | — |

---

## 3. 工具调用的拦截与校验拦截器机制 (Interceptor Specification)

在系统编排与工具中继层，所有模型发起的工具调用都必须经过如下前置钩子校验：

### 3.1 非 Executor 角色写操作钩子
- **检查项**：操作类型是否为 `write_file` / `edit_file` / `delete_file` 或破坏性终端命令。
- **规则**：
  - 如果目标文件所在路径不在 `docs/` 下且不是根目录 `*.md`：
    - **立即拦截**并返回错误：`PermissionDenied: Non-Executor roles are restricted to documentation modifications only.`

### 3.2 Executor 授权钩子
- **检查项**：当前操作是否涉及源码修改或终端执行。
- **规则**：
  - 如果当前上下文中的 `user_approval_granted` 标志为 `False`：
    - 允许生成 `docs/plans/`、`docs/contracts/` 等目录下的规划文档；
    - 拦截一切对 `src/`、`config/` 等业务代码的写操作，并抛出：`AwaitingUserConsent: Non-documentation modification requires explicit user confirmation.`
  - `user_approval_granted` 标志的授予来源：
    - **用户本人明确回复**；
    - **Opus 代理判定**：授权申请发出 5 分钟无用户回应，且事项非安全、非成本相关时，由 Supreme Arbiter（Claude Opus 5）代为判定并置位该标志（授权日志标注"Opus 代理"；安全/成本相关事项禁止代判）。

### 3.3 评审隔离钩子
- Kimi K3 与 Claude Sonnet 5 的写入仅允许落盘到各自专属评审档案（`docs/reviews/{model}/**`），互不可见；
- 若检测到任一 Reviewer 读取或写入对方评审档案，该轮评审作废，由 Project Planner 重新发起。

### 3.4 Supreme Arbiter 钩子
- Opus 仅允许在升级流程被激活后写入 `docs/decisions/**`，或执行 §3.2 规定的授权代判；
- 对其余工具调用除只读外一律拦截，防止其介入普通任务。

---

## 4. 违规与降级处理流程
1. 若任意模型尝试绕过拦截（如尝试通过命令执行覆写源码），Project Planner 将立即终止当前 Turn。
2. 记录违规日志，并向用户抛出警告弹窗/确认框。
3. 经用户审核确认后，方可重置状态或由用户直接指示修复方向。

---

## 5. 文档目录与角色归属规范

```
docs/
├── plans/           # Project Planner: 里程碑计划、任务 DAG、正式方案
├── discussions/     # 需求与设计委员会: 三视角研讨记录
├── contracts/       # Tech Lead: Function Contract、Fix Task
├── reviews/         # Tech Lead 内部 Review + Kimi/Sonnet 独立评审档案
│   ├── kimi/        #   仅 Moonshot Kimi K3 可写
│   ├── sonnet/      #   仅 Claude Sonnet 5 可写
│   ├── internal/    #   Tech Lead 内部评审记录
│   └── vision-qa/   #   仅视觉核验员可写（图像核验报告）
├── decisions/       # Planner 仲裁记录、Opus 裁决、授权日志
└── raw_responses/   # 各模型原始输出留档
```