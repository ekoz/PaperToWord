# 写后快速检查

在 `Write` / `Edit` / `MultiEdit` 之后由 runtime hook 自动执行；本节说明检查内容与处理方式。

## 自动执行

| 检查 | 脚本 | 行为 |
|------|------|------|
| 架构不变量 | `docs/harness/linters/invariant-check.py` | 零第三方依赖、using 白名单、网络/DB/MQ 符号、UTF-8 BOM 风险；硬性违规退出码 1 |
| Spec 同步 | `docs/harness/linters/spec-sync-check.py` | 活跃 feature 的 `spec.md` / `tasks.md` / `verify-report.md` 缺失提醒；仅 warning |

## 收到结果后

1. **VIOLATION**：停止继续扩展，确认是否确属架构变更。
   - 引入第三方依赖、网络、数据库或 MQ 时，必须在 Spec 中说明动机、影响面与迁移方式，并取得确认后再更新 `docs/harness/architecture/index.md`。
2. **WARNING**：判断是否已越过本次 Spec 范围；若是，先在 `tasks.md` 记录对应任务，再补证据。
3. **spec-sync 提示**：确认本次改动是否已落入某个活跃 feature 的 Spec/Tasks；若无对应 feature，说明是否需要 Propose。
4. 修改业务代码后，确认 `docs/specs/active/{feature}/tasks.md` 的 checkpoint 与实现一致。

## 注意

- Hook 不会因提示自动修改或回滚文件。
- 涉及 `docs/knowledge/**` 的更新应与实际实现一致；当前行为不得顺手写成硬规则。
