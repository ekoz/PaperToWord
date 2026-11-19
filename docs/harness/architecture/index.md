# 架构约束

> 来源：`docs/knowledge/architecture.md`、`docs/knowledge/service-boundary.md` 的扫描结论。
> 标注「已确认约束」的条目来自用户/父流程明确的架构要求；标注「现状」的条目描述当前实现，
> 变更时应评估影响，而不是被隐式视为永久规则。

## 1. 架构风格

- **已确认约束**：单体桌面应用；**单进程、单 exe**，同时提供 WinForms GUI 与 CLI 双入口；不拆分微服务、不引入外部服务。
- **已确认约束**：技术栈固定为 C# / .NET Framework 4.x，界面框架 Windows Forms，图形处理使用 GDI+（System.Drawing）。
- **现状**：四段式结构——入口层 `Program`、表示层 `MainForm`、核心处理层 `Sizer`（编排 `ImageUtil` + `Cutter`）、序列化输出层 `Ooxml` + `ZipWriter` + `DocxBuilder`。
- **现状**：绝大多数类型为 `static` 工具类或 `sealed` 数据/UI 类；无接口、无依赖注入、无配置文件、无日志框架。

## 2. 模块/文件结构

| 路径 | 职责 |
|------|------|
| `Source/Program.cs` | 程序入口；CLI 参数解析（`--out` / `--out=`、多图路径）；图标从清单资源加载；P/Invoke 封装 |
| `Source/MainForm.cs` | 主窗体：工具条、图片列表、预览、参数面板、输出与执行、日志区；拖拽、排序、后台任务与完成弹窗 |
| `Source/Core.cs` | 领域核心：`Config`、`Metrics`、`ImageUtil`、`Cutter`、`Sizer`、`MediaStore`、`DocxBuilder` |
| `Source/Ooxml.cs` | OOXML 部件文本生成与 XML 转义（UTF-8 无 BOM） |
| `Source/Zip.cs` | 极简 ZIP 写入器（store 存储、UTF-8 文件名标志、CRC32） |
| `Source/NatSort.cs` | 文件名自然排序 |
| `Source/app.ico` | 应用图标资源 |
| `dist/` | 手工发布产物：`PaperToWord.exe`、`使用说明.txt`、示例 docx |

依赖方向必须保持单向、无循环：

```
Program ──> MainForm ──> Config / ImageUtil / Cutter / Sizer / DocxBuilder / NatString
                              Sizer ──> MediaStore
                              DocxBuilder ──> Ooxml + ZipWriter
Program ──> Sizer + DocxBuilder（CLI 模式直接复用核心，不经过 UI）
```

## 3. 服务间通信

- **已确认约束**：无网络通信、无 RPC、无 MQ、不监听端口、无 HTTP 服务端。
- 进程内通信：GUI/CLI 与核心之间为普通方法调用；后台线程通过 `Control.BeginInvoke` 回主线程更新 UI。
- 操作系统交互仅限 P/Invoke：`kernel32.dll`（`AttachConsole`、`GetConsoleWindow`）、`user32.dll`（`MessageBeep`）。
- 新增网络/数据库/MQ/服务端能力属于架构级变更，必须在 Spec 中显式提出并取得确认。

## 4. 数据源约束

- **已确认约束**：无数据库、无 ORM、无 SQL、无数据源配置、无缓存中间件、无配置文件。
- 数据只存在两处：进程内内存（处理清单、裁片、`Page`、`MediaStore`）与本地文件系统（输入图片只读；输出 docx 与瞬时临时文件）。
- 输出落盘必须保持原子性：先写同目录 `.esw_<8位GUID>.tmp`，成功后 `File.Move` 到目标；失败清理临时文件。
- 目标文件已存在时不得覆盖，按现状自动改名。

## 5. 服务边界（来自 service-boundary.md）

**本服务负责**：

- 试卷图片汇总、去重、页序整理。
- 按裁切方式与位置裁出 1–2 片，并适配纵向/横向 A4、单片或两片同页。
- EXIF 方向纠正与 DPI 档位缩放，统一输出白底 JPEG。
- 自建 OOXML/ZIP 组装标准 `.docx` 并原子落盘。
- GUI 与 CLI 双入口及结果反馈。

**本服务不负责**：

- 试卷内容理解：不做 OCR、文字识别、纠错、去污、版式还原或答案识别；源图片只读。
- 打印与 PDF：不调用打印机、不生成 PDF、不保证 Word/WPS 之外的呈现。
- 持久化与多用户：无数据库、无配置、无历史记录、无账户/角色/鉴权、无并发协作。
- 对外服务能力：无 HTTP/RPC/MQ，不作为其他系统的服务方。

## 6. 技术栈版本

| 项 | 版本/选型 | 证据 |
|----|----------|------|
| 语言 | C# 5（源码注释声明） | 代码未使用 C# 6+ 语法；无 `csproj` 可核对 `LangVersion` <!-- TODO: 待确认 --> |
| 运行时 | .NET Framework 4.x（Win10/11 自带 4.8） | `Program.cs` 头注释与使用说明 <!-- TODO: 待确认具体目标版本 --> |
| 界面 | Windows Forms（手写控件，无 Designer） | 源码 |
| 图像 | GDI+ / System.Drawing | 源码 |
| 文档 | 自研 OOXML（WordprocessingML） | 源码 |
| 归档 | 自研 ZIP Writer（store，不压缩） | 源码 |
| 第三方依赖 | 无（仅 BCL） | 无 packages.config / NuGet 清单 |
| 构建 | **可复现**：根目录 `build.ps1` 固化完整 csc 命令（含占用预检） | `build.ps1` + `exp/build.md` EXP-B1；CI 跑同一脚本 |
| 测试 | 冒烟测试 `tools/smoke_test.py`（本地与 CI 通用，仅标准库）；**无单元测试工程** | 仓库扫描 |

## 7. 已知架构风险

- 目标框架与语言版本无机器可读配置（无 `.csproj`，靠源码注释与《使用说明》佐证）。
- 无单元测试：裁切几何、自然排序、ZIP/OOXML 正确性仍主要依赖冒烟测试与手工验证。
- 进程级静态可变状态（`MediaStore.Map`、`Program._iconStream`、`ImageUtil._codec`）在单任务模型下安全，并行化前必须重构。
- ZIP store 不压缩，docx 体积偏大；统一转 JPEG 会使 PNG 透明区域变白、GIF 仅取首帧。
