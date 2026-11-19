# CLAUDE.md

## 项目概述

PaperToWord 是一个 C# / .NET Framework 4.x 的 Windows 桌面工具：把手机横版拍摄的试卷图片沿中线裁成
1–2 片，按打印最优方式排成 A4 页面，输出一份标准 `.docx`。项目模式为 **brownfield**，已有 v1.3 可用代码。

- **技术栈**：C# 5 / .NET Framework 4.x（Win10/11 自带 4.8）/ Windows Forms / GDI+ / 自研 OOXML + ZIP 写入
- **架构风格**：单体桌面应用，单进程、单 exe，GUI + CLI 双入口；无数据库、无网络、无 MQ、零第三方依赖
- **业务域**：通用域（独立运行的桌面效率工具）
- **评审侧重**：现有系统兼容性、影响范围、迁移成本

## HelixHarness 导航

## 目录导航

- `docs/harness/index.md`：项目约束入口。
- `docs/knowledge/index.md`：当前知识入口，按“文件/目录、定位、加载时机”选择相关叶子。
- `docs/specs/active/<feature>/`：当前需求；Propose 生成 `spec.md`、`tasks.md`，Apply 生成 `verify-report.md`。
- `docs/specs/completed/<feature>/`：通过 Archive 门禁后归档的三份工件。
- `docs/hooks/`：runtime hooks、写后检查与 CR 清单说明（由命令直接读取，不走索引）。

## 关键约束速览

1. 单体单 exe 双入口：C# / .NET Framework 4.x + WinForms，GUI 与 CLI 共用同一核心；不拆微服务、不引入外部服务。
2. 零第三方依赖：仅使用 .NET Framework BCL；新增 NuGet 包属于架构变更，必须先确认。
3. 无网络 / 无数据库 / 无 MQ：不监听端口、不持久化业务数据；新增相关能力必须先确认。
4. 输出安全：docx 经同目录临时文件原子落盘，目标已存在时自动改名、绝不覆盖；源图片只读。
5. 业务规则不默认固化：`docs/knowledge/domain-model.md` 的 BR-01~BR-16 是当前行为参考，是否长期保持需确认。

## 常用命令

`/helix:init`、`/helix:propose`、`/helix:apply`、`/helix:archive`、
`/helix:ut`、`/helix:bug-fix`、`/helix:update`。
`propose --loop` 按顺序执行 Propose → Apply → Archive。

## Agent 工作规则

读取当前宿主原生入口与项目索引，按任务范围加载约束和知识。完整命令以插件 `commands/` 为准。
保留用户已有内容；实现、验证和归档必须有实际工件证据。Apply 内部执行 Standards 与 Spec 双轴评审。
归档前展示知识修改建议并按用户确认的精确范围更新；提交和推送遵循当前命令的授权规则。

- 加载顺序：先读 `docs/harness/index.md` 与 `docs/knowledge/index.md`，按三列表头只加载命中的叶子；
  `docs/knowledge/project-context.md` 为 propose/apply 必读。
- 当前行为的精确描述属于知识库；不得仅凭现状在 harness 中新增硬规则。
- 修改代码后确认 `docs/specs/active/{feature}/tasks.md` 与 `verify-report.md` 证据同步。
