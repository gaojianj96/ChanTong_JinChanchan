# 工具调用与执行权限安全策略规范 (Tool Invocation & Security Policy)

## 1. 目标与范围
本规范约束工作区中所有 AI 模型代理（包括总体统筹、OpenRouter 接入的所有模型组、以及 Codex CLI 执行端）在进行工具调用（Tool Calling / Function Calling）时的行为与权限范围，确保严格符合项目铁律。

---

## 2. 工具调用权限矩阵

| 代理/模型角色 | 允许调用的工具类别 | 允许操作的目标路径 | 严格禁止的行为 |
| :--- | :--- | :--- | :--- |
| **总体统筹 (Copilot)** | 读/搜/文档写/调度工具 | 文档文件 (`docs/`, `*.md`)、Memory 库 | 直接创建/修改源代码文件 (`*.py`, `*.json`, `*.yaml` 等非文档)，发起未授权的破坏性命令 |
| **基础讨论组**<br>· DeepSeek V4 Flash<br>· MiniMax M3<br>· Qwen 3.7 Flash | 仅限只读工具与文档编辑工具 | `docs/**`, `*.md` | 任何源代码写入/修改工具、终端执行破坏性命令 |
| **高级评审组**<br>· DeepSeek V4 Flash (独立)<br>· Claude Sonnet 5 | 仅限只读工具与 Review 文档记录工具 | `docs/reviews/**`, `*.md` | 任何源码修改、创建补丁文件直接覆盖代码 |
| **仲裁模型**<br>· Claude Fable 5 | 仅限只读工具与裁决记录工具 | `docs/decisions/**`, `*.md` | 直接修改源码 |
| **执行端 (Codex CLI)** | 完整的代码读写与执行工具 | 全工作区（需遵循授权流程） | **在未取得用户明确授权前直接写入/覆盖除 Plan 以外的代码文件** |

---

## 3. 工具调用的拦截与校验拦截器机制 (Interceptor Specification)

在系统编排与工具中继层（`src/orchestrator/`），所有由 OpenRouter 或 Codex CLI 发起的工具调用必须经过如下前置钩子校验：

### 3.1 OpenRouter 模型工具调用钩子
- **检查项**：操作类型是否为 `write_file` / `edit_file` / `delete_file`。
- **规则**：
  - 如果目标文件后缀不属于 `DOC_EXTENSIONS = {'.md', '.txt', '.doc'}` 或路径不在 `docs/` 下：
    - **立即拦截**并返回错误：`PermissionDenied: OpenRouter models are restricted to documentation modifications only.`

### 3.2 Codex CLI 工具调用钩子
- **检查项**：当前操作是否涉及源码修改或终端执行。
- **规则**：
  - 如果当前上下文中的 `user_approval_granted` 标志为 `False`：
    - 允许生成 `docs/plans/` 目录下的规划文档；
    - 拦截一切对 `src/`、`config/` 等业务代码的写操作，并抛出：`AwaitingUserConsent: Non-documentation modification requires explicit user confirmation.`

---

## 4. 违规与降级处理流程
1. 若任意模型尝试绕过拦截（如尝试通过命令执行覆写源码），系统统筹将立即终止当前 Turn。
2. 记录违规日志，并向用户抛出警告弹窗/确认框。
3. 经用户审核确认后，方可重置状态或由用户直接指示修复方向。
