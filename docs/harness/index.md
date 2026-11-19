# 项目约束入口

本文件是 `docs/harness/` 的唯一根索引。按「文件/目录、定位、加载时机」选择需要读取的叶子文件。

## 全局约束摘要（最关键 5 条）

1. **单体单 exe 双入口**：C# / .NET Framework 4.x 的 Windows 桌面工具，WinForms GUI 与 CLI 共用同一核心；不拆微服务、不引入外部服务。
2. **零第三方依赖**：仅使用 .NET Framework BCL；新增 NuGet 包属于架构变更，必须先在 Spec 中确认。
3. **无网络 / 无数据库 / 无 MQ**：不监听端口、不持久化业务数据；新增网络、数据库、消息队列或服务端能力必须先确认架构变更。
4. **输出安全**：docx 必须经同目录临时文件原子落盘，目标已存在时自动改名、绝不覆盖；源图片只读。
5. **业务规则不默认固化**：`domain-model.md` 中的 BR-01~BR-16 是当前行为参考，是否长期保持需用户确认（见 `invariants/biz.md`）。

## 索引

| 文件/目录 | 定位 | 加载时机 |
|-----------|------|---------|
| architecture/index.md | 架构风格、模块结构、通信约束、数据源约束、服务边界、技术栈版本与已知风险 | 涉及架构/模块划分/依赖/构建/通信/数据存储/服务边界时 |
| invariants/biz.md | 业务不变量状态（暂无已确认硬规则）+ 当前行为参考 BR-01~BR-16 | 涉及业务规则/裁切/排版/输出/异常处理时 |
| infrastructure/security.md | 本地文件读写边界、输入校验、输出安全、外部进程启动、依赖与凭据安全 | 涉及文件 I/O/输入校验/安全/依赖时 |
| infrastructure/logging.md | GUI 日志区与 CLI stdout 输出规范、失败反馈、敏感信息与线程约束 | 涉及日志/错误提示/CLI 输出/用户反馈时 |
| linters/invariant-check.py | 零第三方依赖、using 白名单、网络/DB/MQ 引入、UTF-8 BOM 检查 | PostToolUse hook 自动执行；手工运行检查架构约束时 |
| linters/spec-sync-check.py | 校验活跃 feature 的 spec/tasks/verify-report 一致性（warning-only） | PostToolUse hook 自动执行；归档前检查证据时 |

## 约束来源说明

- `architecture/index.md` 中标注「已确认约束」的条目来自父流程明确的架构要求与项目现有规约；标注「现状」的条目描述当前实现。
- 当前实现的精确行为（返回值、默认参数、页面几何、日志文本）记录在 `docs/knowledge/`，不得在未经 Spec 确认时升级为硬规则。
