# CR 检查清单

> 依据：`docs/harness/index.md` 全局约束、`docs/harness/architecture/index.md`、当前 feature 的 `spec.md` 与 `tasks.md`。
> 本清单只汇总已确认约束，不重新定义规则。

## 1. 需求与 Spec

- [ ] 改动对应某个活跃 feature 的 `spec.md`，无未登记的范围外改动。
- [ ] `tasks.md` 中的任务与 checkpoint 已更新，完成项与代码一致。
- [ ] 若改变了 `docs/knowledge/domain-model.md` 中 BR-01~BR-16 的当前行为，Spec 已写明影响范围与迁移方式。

## 2. 代码规范

- [ ] 类型与方法 PascalCase、局部变量 camelCase、私有字段 `_camelCase`；中文注释与 UI 文案保持现有风格。
- [ ] 未引入接口/DI/配置文件/日志框架等现有架构中不存在的结构，除非 Spec 已确认。
- [ ] 未引入第三方依赖、NuGet 包或非 BCL 命名空间。
- [ ] 新增网络、数据库、MQ、服务端能力？必须先取得架构变更确认。
- [ ] 保持依赖方向单向，无循环依赖。

## 3. 业务不变量与兼容性

- [ ] 逐条核对 `invariants/biz.md` 中与本次改动相关的当前行为。
- [ ] 输出路径、重名改名、部分失败继续、统一白底 JPEG 等行为变化已显式确认。
- [ ] 源图片只读；未修改/移动/删除用户文件。

## 4. 安全与文件系统

- [ ] 输入扩展名白名单与解码校验未被绕过。
- [ ] 输出仍经 `.esw_*.tmp` 原子落盘；失败清理临时文件。
- [ ] 不覆盖已有文件；输出目录按规则创建。
- [ ] 外部进程启动仅限用户确认后的结果打开。

## 5. 线程与资源

- [ ] UI 更新经 `Control.BeginInvoke` 回到主线程。
- [ ] GDI+ `Image` / `Bitmap` / 流按实际所有权释放；`MediaStore` 在任务结束清理。
- [ ] 未破坏"同一时刻只允许一个生成任务"的防重入约束。

## 6. 验证与证据

- [ ] `verify-report.md` 记录了实际执行的测试/手工验证与结果。
- [ ] Standards 与 Spec 双轴评审均已完成，finding 已对账（修复/接受/延后均有说明）。
- [ ] linter 无 VIOLATION；WARNING 已解释。

## 7. 构建与发布

- [ ] 构建方式若仍不可复现，改动未声称已通过自动化构建。
- [ ] `dist/` 发布产物未在未授权情况下被覆盖。
- [ ] 版本标识（`AppTitle`、`app.xml` 中的版本）变更已确认。<!-- TODO: 待确认应用版本与 AppVersion 是否需对齐 -->
