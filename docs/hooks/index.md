# Hook 机制总览

HelixHarness 在本仓库使用三类机制，职责不同，不得互相替代：

| 类型 | 载体 | 触发时机 | 作用 |
|------|------|---------|------|
| Runtime hooks | `.claude/settings.json` → `hooks.PostToolUse` | 每次 `Write` / `Edit` / `MultiEdit` 之后 | 自动执行两个 linter，快速反馈 |
| Workflow gates | `/helix:apply`、`/helix:archive` 在 `docs/specs/active/{feature}/verify-report.md` 中记录 | 需求阶段转换时 | 测试、双轴评审、证据门禁 |
| Review checklists | `docs/hooks/cr-checklist.md` | Standards 与 Spec 评审时 | 人工评审清单 |

## Runtime hooks

- 匹配器：`Write|Edit|MultiEdit`（唯一一条，去重）。
- 命令：
  - `docs/harness/linters/invariant-check.py` — 架构约束检查；硬性违规时退出码 1。
  - `docs/harness/linters/spec-sync-check.py` — spec/tasks/verify-report 一致性提醒；warning-only，始终退出 0。
- Hook 只做检查与提示，不修改文件、不提交、不上报。

## Workflow gates

- 不生成独立的 `docs/harness/quality-gates/` 目录。
- Apply 完成时在 `verify-report.md` 记录测试与双轴评审证据；Archive 校验证据后归档。
- Init 不创建 feature、不生成验证报告。

## Review checklists

- `docs/hooks/cr-checklist.md` 供代码评审使用，引用 harness 约束与当前 Spec，不重复定义规则。
