# 技术栈与架构全景

> 生成时间：2026-11-19
> 项目：PaperToWord v1.3 —— 试卷裁页成 Word 的 Windows 桌面工具
> 扫描范围：`Source/` 全部源码、`dist/使用说明.txt`、仓库根目录构建元数据

## 1. 技术栈总览

| 维度 | 技术选型 | 版本 | 说明 |
| :--- | :--- | :--- | :--- |
| 语言 | C# | 5（源码头注释声明） | 代码未使用 C# 6+ 语法（无字符串插值、无 `?.`）；无 `csproj` 可核对 `LangVersion` |
| 运行时 | .NET Framework | 4.x（Win10/11 自带 4.8） | 依据 `Program.cs` 头注释与《使用说明》；无 `TargetFramework` 配置可核对 |
| 构建 | 根目录 `build.ps1` | 系统 csc.exe | 无 `.sln`/`.csproj`（刻意如此，保证零依赖可复现）。`build.ps1` 固化完整命令：`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:anycpu /optimize+ /out:dist\PaperToWord.exe /win32icon:Source\app.ico /resource:Source\app.ico,AppIcon` + BCL 四引用 + 6 个源文件。详见 `exp/build.md` |
| 界面框架 | Windows Forms | - | `System.Windows.Forms`；窗体与控件全部手写，无 `.Designer.cs` / `.resx` |
| 图形 / 图像 | GDI+（System.Drawing） | - | 图片加载、EXIF 方向修正、双三次插值缩放、JPEG 编码、预览绘制 |
| 文档生成 | 自研 OOXML 生成器 | OOXML（WordprocessingML） | 手写 `word/document.xml` 等部件，非 Office SDK / OpenXML SDK |
| 归档 | 自研 ZIP Writer | ZIP 2.0（store 存储） | 自带 CRC32，明确不依赖 `System.IO.Compression` |
| 第三方依赖 | 无 | - | 仅使用 BCL；仓库无 `packages.config` / `Directory.Build.props` / NuGet 清单 |
| 平台互操作 | P/Invoke | - | `kernel32.dll`（`AttachConsole`、`GetConsoleWindow`）、`user32.dll`（`MessageBeep`） |
| 并发 | System.Threading.Thread | - | 后台处理线程 + `BeginInvoke` 回主线程更新 UI |
| 数据 / 中间件 | 无 | - | 无数据库、无缓存、无 MQ、无 HTTP 服务、无配置文件、无端口与环境变量 |
| 监控 | 无 | - | 仅界面上 `RichTextBox` 日志与完成弹窗 |

## 2. 架构设计

### 2.1 整体架构

**单体桌面应用（单进程、单 exe、GUI + CLI 双入口）**，无模块拆分、无外部服务。

| 文件 | 职责 |
| :--- | :--- |
| `Source/Program.cs` | 程序入口；CLI 参数解析（`--out` / `--out=`、多图路径）；图标从清单资源加载；`Native` P/Invoke 封装 |
| `Source/MainForm.cs` | 主窗体：顶部工具条、左列表、右预览、参数面板、底部输出与执行、日志区；拖拽、排序、预览绘制、后台任务与完成弹窗 |
| `Source/Core.cs` | 领域核心：`Config`（参数）、`Metrics`（版面尺寸常量）、`ImageUtil`（读图/EXIF/缩放/JPEG）、`Cutter`（裁片矩形与 EMU 适配）、`Sizer`（页序列构建）、`MediaStore`（构建期图片暂存）、`DocxBuilder`（打包与原子落盘） |
| `Source/Ooxml.cs` | OOXML 部件文本生成：`[Content_Types].xml`、`.rels`、`document.xml`、`styles.xml`、`settings.xml`、`core.xml`、`app.xml`；XML 转义与 UTF-8（无 BOM）编码 |
| `Source/Zip.cs` | 极简 ZIP 写入器：local file header / central directory / EOCD，store 方式，UTF-8 文件名标志位，CRC32 查表实现 |
| `Source/NatSort.cs` | 文件名自然排序（数字段按数值比较，如“第2页”在“第10页”前），非数字部分忽略大小写 |
| `Source/app.ico` | 应用图标资源 |

依赖方向（单向，无循环）：

```
Program ──> MainForm ──> Config / ImageUtil / Cutter / Sizer / DocxBuilder / NatString
                                    Sizer ──> MediaStore
                                    DocxBuilder ──> Ooxml + ZipWriter
Program ──> Sizer + DocxBuilder（CLI 模式直接复用核心，不经过 UI）
```

### 2.2 代码分层

不属于经典 Controller/Service/Repository，也不是 DDD/CQRS，而是面向单文件桌面工具的**四段式结构**：

1. **入口层**：`Program`（CLI 解析、控制台附加、GUI 启动）。
2. **表示层**：`MainForm`（控件装配、拖拽、预览 GDI+ 绘制、线程调度、日志）。
3. **核心处理层**：`Sizer` 编排 `ImageUtil` + `Cutter`，产出纯数据模型 `Page`（`RelIds` / `EmuW` / `EmuH` / `Names` / `Landscape`）与 `Slice`。
4. **序列化输出层**：`Ooxml` 生成 XML 部件，`ZipWriter` 打包，`DocxBuilder` 负责临时文件与原子改名。

特征：

- 绝大多数类型为 `static` 工具类或 `sealed` 数据/UI 类；**无接口、无依赖注入、无配置文件、无日志框架**。
- 存在少量进程级静态可变状态：`MediaStore.Map`（图片暂存）、`Program._iconStream`（图标流保活）、`ImageUtil._codec`（JPEG 编码器缓存）；每次生成前后 `MediaStore.Clear()`。
- 参数集中在 `Config` 类中由 UI 或 CLI 构造后传入 `Sizer.Build`。

数据流：

```
图片路径[]
  → ImageUtil.Load（ReadAllBytes + MemoryStream，EXIF Orientation 修正，尺寸校验）
  → DownscaleIfNeeded（长边上限 MaxSourceSide=4600px）
  → Cutter.Plan（Auto/LR/TB/None，按百分比计算 1 或 2 个 Rectangle）
  → ImageUtil.MakeSlice（按目标 DPI 计算像素盒，双三次缩放，JPEG 编码）
  → Sizer.Build（组装 Page；横图不裁时可整页转横向 A4）
  → MediaStore.Put（word/media/imageN.jpg）
  → DocxBuilder.Write（Ooxml 各部件 + ZipWriter → 临时文件 → File.Move 原子改名）
```

版面尺寸约定（`Metrics` / `Ooxml.SectPr`）：

- A4 纵向 210×297mm，四边页边距 5mm；横向 297×210mm。
- OOXML 中 `w:pgSz` 使用 twip：纵向 `11906×16838`，横向 `16838×11906 w:orient="landscape"`，页边距 `283` twip。
- EMU 换算常量 `MmToEmu = 36000`；图片按版心等比适配，单片填充率默认 0.96，两片同页时 0.98 且高度减 3mm 间隔。
- 每页一个 `w:sectPr` 分节，保证每张裁片独占一页；最后一节为文档级 `sectPr`。

docx 包内固定部件：`[Content_Types].xml`、`_rels/.rels`、`docProps/core.xml`、`docProps/app.xml`、`word/styles.xml`、`word/settings.xml`、`word/document.xml`、`word/_rels/document.xml.rels`、`word/media/imageN.jpg`。

### 2.3 服务间通信

**无网络通信、无 RPC、无 MQ**。通信均发生在进程内：

- GUI/CLI 与核心之间为普通方法调用。
- 后台线程与 UI 线程通过 `Control.BeginInvoke` 投递委托。
- 与操作系统通过 P/Invoke 交互：`GetConsoleWindow` + `AttachConsole(-1)` 让 GUI 子系统程序在命令行启动时复用父控制台输出；`MessageBeep` 做完成提示音。

## 3. 工程规范

| 规范项 | 工具 | 配置文件 | 说明 |
| :--- | :--- | :--- | :--- |
| CI/CD | GitHub Actions | `.github/workflows/build.yml` | push（main/master）与 PR：架构不变量检查 → `build.ps1` 编译 → `tools/smoke_test.py` 冒烟；打 `v*` tag 自动发布 GitHub Release（exe 不入库，只作 Release 附件） |
| 代码规范 | 无自动检查 | - | 无 `.editorconfig` / `checkstyle` / StyleCop / Roslyn Analyzer；实际风格：类型与方法 PascalCase、局部变量 camelCase、私有字段 `_camelCase`、中文注释与中文 UI 文案 |
| 容器化 | 无 | - | 无 `Dockerfile` / `docker-compose.yml` / `k8s/`；交付物为单 exe（绿色版） |
| 测试 | 冒烟脚本 | `tools/smoke_test.py` | 仅标准库：合成一张横版试卷 PNG → 调 exe CLI 生成 docx → 校验 zip CRC / 必需部件 / XML 可解析 / 图片与分节数量；另含"全部图片失败 → 退出码 1 且不产出空文档"与"目标重名 → 自动改名 `_2`、绝不覆盖"两条回归。无单元测试工程 |
| 依赖管理 | 无 | - | 无 NuGet 清单，零第三方包 |
| 版本标识 | 源码常量 | `Program.cs` | `AppTitle = "PaperToWord v1.3"`；docx `app.xml` 中 `AppVersion` 为 `1.2000` <!-- TODO: 待确认是否需与应用版本 v1.3 对齐 --> |
| 发布产物 | 手工 | `dist/` | `PaperToWord.exe`、`使用说明.txt`、`示例输出_时间在哪里.docx` |

### 3.1 错误处理与健壮性约定

- **单图失败不中断整体**：`Sizer.Build` 逐图 `try/catch`，失败项写入 `errors` 并继续；仅当全部失败时 UI/CLI 报错。
- **非关键路径异常吞掉**：图标加载、EXIF 修正、视觉样式设置、日志追加等使用 `try { } catch { }`，避免影响主流程。
- **原子输出**：先写同目录 `.esw_xxxx.tmp`，成功后 `File.Move` 到目标；失败时删除临时文件再抛出。
- **重名/占用自动改名**：`DocxBuilder.PickFreePath` 依次尝试 `name_2.docx` … `name_199.docx`，兜底追加 `HHmmss`。
- **输入校验**：仅接受白名单扩展名（jpg/jpeg/png/bmp/gif/tif/tiff）；图片小于 8×8 像素判为无效；不可识别图片抛 `InvalidDataException`。
- **编码约定**：源码 UTF-8；XML 统一 `UTF-8` 无 BOM；XML 特殊字符转义，控制字符（除 `\t`）替换为空格，避免生成非法 XML。
- **资源生命周期**：`ImageUtil.Load` 把 `MemoryStream` 挂到 `Image.Tag`，规避 GDI+ 流提前回收；`DownscaleIfNeeded` 产生新对象时由调用方按 `owned` 标志 `Dispose`；任务结束 `MediaStore.Clear()` 并 `GC.Collect()`。

### 3.2 已知缺口与风险

- **构建可复现**：完整命令已固化到根目录 `build.ps1`（系统自带 csc.exe，含占用预检）。图标双参数缺一不可：`/win32icon` 设 PE 图标（资源管理器），`/resource:app.ico,AppIcon` 供 `GetManifestResourceStream("AppIcon")` 读取（窗口/任务栏）。新增 `.cs` 需同步加入 `build.ps1` 的源文件清单。
- **目标框架不可核对**：`.NET Framework 4.x` 结论来自源码注释与《使用说明》，无机器可读配置。
- **无单元测试**：ZIP/OOXML 与两条 CLI 回归由 `tools/smoke_test.py` 覆盖；裁切几何、自然排序、GUI 交互仍依赖手工验证。
- **ZIP 不压缩**：store 模式使 docx 体积偏大，换取实现简单与 Office 兼容性。
- **图片一律转 JPEG**：PNG 透明区域以白色填充，适合打印场景；GIF 多帧仅取首帧（`Image.FromStream` 行为）。
- **静态可变状态**：`MediaStore` 为进程级单例，GUI 以 `_busy` 防重入，CLI 每次进程一次调用；当前单任务模型下安全，未来若并行化需重构为实例状态。

## 4. 运行与使用形态

- **GUI 模式**：无参数启动，拖入图片/文件夹 → 调整顺序与参数 → 生成 docx → 弹窗询问是否打开。
- **CLI 模式**：参数含图片路径或 `--out` 时进入；`--out=路径` 或 `--out 路径` 指定输出，缺省输出到首图目录下 `yyyyMMdd-.docx`；只给文件名时落到首图所在目录；退出码 1 表示失败。
- **运行环境**：Windows 10/11，自带 .NET Framework 4.8，离线可用，无需 Office/WPS。

<!-- 已确认：构建命令见根目录 build.ps1；编译器为系统自带 .NET Framework 4.x csc.exe（C# 5 语法上限）；目标运行时 .NET Framework 4.8（Win10/11 自带）。详见 exp/build.md -->
