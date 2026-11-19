# 桌面程序入口与进程内契约（非对外 API）

> 协议：无（Windows 本地进程内调用 / 命令行 / WinForms 事件）
> 入口类：`Program`、`MainForm`
> 说明：本文件列出的内容**不是** HTTP REST / Thrift / gRPC / Dubbo 接口，不监听网络端口、无鉴权与序列化协议语义，不得作为对外 API 使用。列出这些入口仅用于描述程序边界。

---

## 入口列表

| 序号 | 入口 | 触发方式 | 说明 |
| :--- | :--- | :--- | :--- |
| 1 | 命令行入口 | 进程启动参数 | `PaperToWord.exe <图片...> [--out=<docx>]` 批量转换 |
| 2 | 文件拖放入口 | WinForms DragDrop | 拖入图片或文件夹追加到处理列表 |
| 3 | GUI 控件事件 | 按钮 / 列表 / 参数控件 | 添加、排序、预览、执行 |
| 4 | 进程内核心契约 | 静态类公共方法 | `Sizer.Build`、`DocxBuilder.Write`、`ImageUtil`、`Cutter`、`NatString` |
| 5 | 文件 I/O 边界 | 本地文件系统 | 输入图片、输出 docx、临时文件 |

---

## 1. 命令行入口 `PaperToWord.exe` — 批量转换

**入口**：`Program.Main` → `Program.TryParseCli` → `Program.RunCli`
**鉴权**：无（本地进程）
**描述**：无参数启动时打开 WinForms 主窗口；判定为命令行模式时直接读取图片并生成 docx，不显示窗口。

### 请求参数（进程 argv）

| 位置 | 参数名 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- | :--- |
| 位置参数 | `files` | `string[]` | 条件必填 | 一个或多个图片路径；每个参数必须 `File.Exists` 为真才会被收录，随后转为绝对路径。注意：此阶段不校验扩展名，非图片文件会在后续读图阶段失败并被跳过 |
| 选项 | `--out=<path>` | string | 否 | 输出 docx 路径；取 `--out=` 之后的内容 |
| 选项 | `--out <path>` | string | 否 | 输出 docx 路径；取下一个参数作为值 |
| 选项 | 其他以 `-` 开头的参数 | string | 否 | 标记为未知参数（`unknownFlag`），仅影响模式判定，不报错 |

**模式判定规则**（`TryParseCli`）

| 条件 | 结果 |
| :--- | :--- |
| `args` 为空或 null | 图形界面模式 |
| 出现 `--out` / `--out=` | 命令行模式（无论是否有文件） |
| 无 `--out`，且至少一个有效文件、无未知参数 | 命令行模式 |
| 无 `--out`，且无有效文件 / 存在未知参数 | 图形界面模式 |

**输出路径解析**

| 场景 | 实际输出路径 |
| :--- | :--- |
| `--out` 缺失或值为空 | `第一张图片所在目录\yyyyMMdd-.docx`（例如 `20261119-.docx`） |
| `--out` 只给文件名（无目录部分） | `第一张图片所在目录\<给定文件名>` |
| `--out` 给了带目录的路径 | 原样使用（若目录不存在会自动创建） |
| 目标文件已存在 | 自动追加 `_2`、`_3` … `_199`，仍冲突则追加 `_HHmmss` |

**固定处理参数**：命令行模式使用 `new Config()` 的默认值（见第 4 节），不解析裁切方式、DPI、排布等选项。

### 响应（控制台 stdout）

| 顺序 | 输出行 | 类型 | 说明 |
| :--- | :--- | :--- | :--- |
| 1 | `PaperToWord v1.3（命令行模式）` | string | 固定标题 |
| 2 | `输出: <最终 docx 绝对路径>` | string | 实际写入路径（可能因重名被改名） |
| 3 | `页数: N`，或 `页数: N，跳过 M 张失败图片` | string | N 为生成的 A4 页数；M 为读图失败被跳过的张数 |
| 失败 | `失败: <异常消息>` | string | 例如 `没有有效的图片文件（检查路径是否加引号）`、`全部图片处理失败`、I/O 异常信息 |

**退出码**：成功 `0`；捕获到异常时 `Environment.ExitCode = 1`。
**副作用**：若进程无控制台窗口，调用 `AttachConsole(-1)` 挂接父控制台；结束时调用 `MessageBeep` 提示音；写出一份 .docx。

---

## 2. 文件拖放入口（WinForms DragDrop）

**入口**：`MainForm.MainForm_DragEnter` / `MainForm.MainForm_DragDrop`
**绑定控件**：`MainForm`、`_list`（图片列表）、`_previewHost`（预览面板）、`_optsPanel`（参数面板）
**描述**：接受 Windows 文件拖放，把图片追加到处理列表。

### 入参

| 字段 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- |
| `e.Data` 中的 `DataFormats.FileDrop` | `string[]` | 是 | 拖入的文件或目录路径数组；不含该格式时效果为 `None` |
| 目录条目 | `Directory.GetFiles(path)` | — | 仅展开**顶层**文件，不递归子目录 |
| 文件条目 | `string` | — | 扩展名需在允许列表内，见下表 |

**允许的图片扩展名（`ImageUtil.AllowedExts`）**

| 扩展名 | 说明 |
| :--- | :--- |
| `.jpg` / `.jpeg` | JPEG |
| `.png` | PNG |
| `.bmp` | BMP |
| `.gif` | GIF |
| `.tif` / `.tiff` | TIFF |

**追加规则**：全路径去重（`OrdinalIgnoreCase`）；每次追加后按文件名自然排序；保留原选中项。

### 结果

| 结果 | 反馈 |
| :--- | :--- |
| 新增成功 | 列表刷新，日志 `已添加 N 张（重复 M 张已跳过），按文件名智能排序…` |
| 全部重复 | 日志 `这些图片已经在列表里了。` |
| 无可识别图片 | 日志 `没有可识别的图片（支持 jpg/png/bmp/gif/tif）。` |
| 拖放异常 | 日志 `拖放失败：<消息>` |

---

## 3. GUI 控件事件

### 3.1 操作控件

| 控件 | 事件 | 行为 | 副作用 |
| :--- | :--- | :--- | :--- |
| `添加图片`（`_btnAdd`） | Click | 打开多选文件对话框（Filter 见 `ImageUtil.FilterText`） | 调用 `AddFiles` |
| `移除所选`（`_btnRemove`） | Click | 删除列表中所有选中项（支持多选） | 列表刷新、预览失效 |
| `清空`（`_btnClear`） | Click | 清空图片列表 | 列表刷新 |
| `智能排序`（`_btnSort`） | Click | 按 `NatString.CmpPath` 自然排序（数字按数值比较） | 日志提示 |
| `上移 ↑`（`_btnUp`） | Click | 选中项整体上移一位，保持多选 | 列表刷新并恢复选中 |
| `下移 ↓`（`_btnDown`） | Click | 选中项整体下移一位，保持多选 | 列表刷新并恢复选中 |
| `浏览…`（`_btnOut`） | Click | 打开保存对话框（Filter `Word 文档 (*.docx)|*.docx`） | 回填输出路径 |
| `▶ 执行：裁开并生成 Word`（`_btnRun`） | Click | 后台线程执行裁切与 docx 生成 | 完成后弹窗、写文件、可打开结果 |
| `数学` / `语文` / `英语` | Click | 输出框填为 `yyyyMMdd-shuxue.docx` / `-yuwen.docx` / `-en.docx` | 已是完整路径时仅替换文件名部分 |
| 图片列表（`_list`） | SelectedIndexChanged | 刷新预览区 | 加载并缓存当前图片 |
| 预览面板（`_previewHost`） | Paint | 绘制原图、裁切线（红色虚线）或纸型示意 | 只读渲染 |

### 3.2 参数控件到 `Config` 的映射

| 控件 | 可选值 | 映射字段 | 映射值 |
| :--- | :--- | :--- | :--- |
| 裁切方式（`_mode`） | 自动：横版裁 / 强制左右裁 / 强制上下裁 / 不裁切 | `Config.Mode` | `Auto=0` / `LR=1` / `TB=2` / `None=3` |
| 清晰度（`_quality`） | 150 DPI / 200 DPI / 300 DPI 推荐 / 原图(大) | `Config.Quality` | `150` / `200` / `300` / `0`（`0` 时同时置 `MaxSourceSide=0`） |
| 页面排布（`_layout`） | 每片一页 推荐 / 两片同页 | `Config.TwoUp` | `false` / `true` |
| 裁切位置（`_pos`） | 滑杆 30–70（默认 50） | `Config.Position` | 百分比数值；真正参与计算时被限制在 20–80 |
| 不裁切的横图自动转横向 A4（`_chkLandscape`） | 勾选 / 未勾选 | `Config.AutoLandscape` | `true` / `false` |

### 3.3 执行流程

**入口**：`MainForm.RunInBackground`（后台线程）

| 步骤 | 调用 | 说明 |
| :--- | :--- | :--- |
| 1 | 校验列表非空 | 为空时弹出"还没有图片"提示并返回 |
| 2 | 解析输出路径 | 留空 → 第一张图片目录 + `yyyyMMdd-.docx`；只给文件名 → 拼到第一张图片目录；无 `.docx` 后缀则自动补齐 |
| 3 | `MediaStore.Clear()` | 清理上次构建的媒体缓存 |
| 4 | `Sizer.Build(files, cfg, log, out errors)` | 逐张读图、裁切、缩放、JPEG 编码 |
| 5 | `DocxBuilder.Write(outPath, pages)` | 生成 docx，返回实际路径 |
| 6 | UI 回调 | 更新输出框与日志；弹窗提示；`MediaStore.Clear()`、`GC.Collect()` |

**失败反馈**：全部图片失败时抛出 `InvalidOperationException("全部图片处理失败：\n" + 明细)`；其余异常以 `失败：<消息>` 显示。

---

## 4. 进程内核心契约

### 4.1 `Config`（排版/裁切配置）

| 字段名 | 类型 | 默认值 | 取值范围 / 说明 |
| :--- | :--- | :--- | :--- |
| `Mode` | `SplitMode` | `Auto` | 裁切方式，枚举见 4.2 |
| `Position` | `double` | `50.0` | 裁切线位置百分比；`Cutter.Plan` 内限制到 20–80 |
| `Quality` | `int` | `300` | 目标 DPI；`0` = 不降采样（使用原图分辨率） |
| `TwoUp` | `bool` | `false` | `true`：一张原图裁出的两片上下排进同一页 A4 |
| `FillRatio` | `double` | `0.96` | 单片占版心比例（两片同页时固定用 0.98） |
| `MaxSourceSide` | `int` | `4600` | 输入图片长边降采样上限（像素）；`0` = 不降采样 |
| `AutoLandscape` | `bool` | `true` | 未裁切的横图自动改用横向 A4 撑满 |

### 4.2 `SplitMode` 枚举

| 值 | 名称 | 含义 |
| :--- | :--- | :--- |
| 0 | `Auto` | 宽 > 高的横图沿竖中线左右裁；竖图不裁切 |
| 1 | `LR` | 强制沿竖中线左右裁切 |
| 2 | `TB` | 强制沿水平中线上下裁切 |
| 3 | `None` | 不裁切，整图放入一页 |

### 4.3 `Sizer.Build` — 构建页序列

```csharp
public static List<Page> Build(string[] files, Config cfg, Action<string> log, out List<string> errors)
```

| 参数 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- |
| `files` | `string[]` | 是 | 图片文件路径数组，按列表顺序处理 |
| `cfg` | `Config` | 是 | 裁切与排版配置 |
| `log` | `Action<string>` | 否 | 逐图进度回调；为 `null` 时不输出 |
| `errors` | `out List<string>` | 是 | 失败记录，格式 `<文件名>：<异常消息>` |

**返回**：`List<Page>`，每张 A4 页对应一个 `Page` 对象。
**逐图异常策略**：单张失败只记录到 `errors` 并继续处理后续图片，不中断整批。

**日志文本格式**

| 场景 | 文本 |
| :--- | :--- |
| 两片同页 | `<文件名>  →  1 页（上下两片）` |
| 单片 | `<文件名>  →  1 页（横向撑满）` 或 `<文件名>  →  1 页` |
| 裁成两片 | `<文件名>  →  2 页` |
| 失败 | `跳过 <文件名>（<异常消息>）` |

### 4.4 `Page` — 一页（一个分节）

| 字段名 | 类型 | 说明 |
| :--- | :--- | :--- |
| `RelIds` | `List<string>` | 图片关系 ID 列表，如 `rId1`、`rId2` |
| `EmuW` | `List<long>` | 每张图的显示宽度（EMU，1mm = 36000 EMU） |
| `EmuH` | `List<long>` | 每张图的显示高度（EMU） |
| `Names` | `List<string>` | 图片替代文本/名称，如 `左半页 · 001.jpg` |
| `Landscape` | `bool` | `true` 时该节使用横向 A4（297×210mm） |

> 页数由 `pages.Count` 决定，每个 `Page` 生成一个 Word 分节（`sectPr`），实现"每片独占一页"。

### 4.5 `DocxBuilder.Write` — 生成 docx

```csharp
public static string Write(string desiredPath, List<Page> pages)
```

| 参数 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- |
| `desiredPath` | `string` | 是 | 期望输出路径；父目录不存在时自动创建 |
| `pages` | `List<Page>` | 是 | 由 `Sizer.Build` 构建的页序列 |

**返回**：实际写入的绝对路径（重名或占用时可能已自动改名）。
**写入策略**：先写同目录临时文件 `.esw_<8位GUID>.tmp`，成功后删除同名旧文件并 `File.Move` 原子替换；异常时清理临时文件并重新抛出。
**重名规则**：`<stem>_2.docx` … `<stem>_199.docx`，仍冲突时 `<stem>_HHmmss.docx`。

**docx 包内部件**

| 部件路径 | 内容 |
| :--- | :--- |
| `[Content_Types].xml` | OOXML 内容类型声明 |
| `_rels/.rels` | 包级关系 |
| `docProps/core.xml` | 标题"试卷（裁页版）"、作者/最后修改者 `PaperToWord`、创建/修改时间 |
| `docProps/app.xml` | 应用程序名 `PaperToWord`、`AppVersion 1.2000` |
| `word/styles.xml` | 默认字体 Times New Roman / 宋体，字号 21 半磅 |
| `word/settings.xml` | 缩放 100%、兼容模式 15 |
| `word/document.xml` | 正文：每页一个居中图片段落 + 分节符 |
| `word/_rels/document.xml.rels` | 图片、样式、设置关系 |
| `word/media/imageN.jpg` | 裁切/压缩后的 JPEG 图片 |

**页面几何**

| 属性 | 纵向 A4 | 横向 A4 |
| :--- | :--- | :--- |
| 页面尺寸 | 210 × 297 mm | 297 × 210 mm |
| 页边距（四边） | 5 mm（283 twips） | 5 mm |
| 版心 | 200 × 287 mm | 287 × 200 mm |
| 两片同页单片可用高度 | — | — |

### 4.6 `ImageUtil` — 图片读取与编码

| 方法 | 签名 | 说明 |
| :--- | :--- | :--- |
| `IsAllowed` | `static bool IsAllowed(string path)` | 扩展名是否在 `.jpg/.jpeg/.png/.bmp/.gif/.tif/.tiff` 内 |
| `FilterText` | `static string FilterText()` | 文件对话框过滤器文本 |
| `Load` | `static Image Load(string path)` | 读入内存流（不锁定文件），按 EXIF `0x0112` 修正 0–8 方向；尺寸小于 8×8 抛 `InvalidDataException("图片尺寸过小")`；无法解析抛 `InvalidDataException("不是可识别的图片文件")` |
| `DownscaleIfNeeded` | `static Image DownscaleIfNeeded(Image img, int maxSide)` | 长边超过 `maxSide` 时等比缩小（高质量双三次插值）；`maxSide <= 0` 或未超限时返回原图 |
| `ToJpeg` | `static byte[] ToJpeg(Bitmap bmp, int quality)` | 以指定 JPEG quality 编码到内存 |
| `MakeSlice` | `static Slice MakeSlice(Image src, Rectangle rect, int dpi, bool halfStack, bool landscape, int jpegQuality)` | 裁切指定矩形并按目标 DPI/版心缩放，返回 `Slice` |

**`Slice` 字段**：`W`（`int`，像素宽）、`H`（`int`，像素高）、`Jpeg`（`byte[]`，编码后数据）。
**JPEG 质量选择（`Sizer.Build`）**：`Quality <= 0` → 92；`Quality >= 300` → 88；其余 → 85。

### 4.7 `Cutter` — 裁切几何计算

| 方法 | 签名 | 说明 |
| :--- | :--- | :--- |
| `Plan` | `static List<Rectangle> Plan(int W, int H, SplitMode mode, double pct)` | 返回 1 或 2 个裁切矩形；`pct` 限制到 20–80；`Auto` 仅在 `W > H` 时左右裁切 |
| `IsVertical` | `static bool IsVertical(List<Rectangle> cuts)` | 两个裁片是否上下同顶（即左右裁切） |
| `FitEmu` | `static void FitEmu(int pxW, int pxH, long maxW, long maxH, double ratio, out long w, out long h)` | 按比例放入 `maxW × maxH` 的 EMU 盒，输出实际宽高 |

### 4.8 `NatString` — 文件名自然排序

| 方法 | 签名 | 说明 |
| :--- | :--- | :--- |
| `CmpPath` | `static int CmpPath(string a, string b)` | 先按文件名自然比较，再按完整路径 `OrdinalIgnoreCase` 比较 |
| `Compare` | `static int Compare(string s1, string s2)` | 逐字符比较；连续数字段按数值大小比较（"第2页" < "第10页"） |

### 4.9 `Ooxml` / `ZipWriter` — 文档封装

| 类型 | 公共方法 | 说明 |
| :--- | :--- | :--- |
| `Ooxml` | `Utf8`、`Esc`、`Document`、`SectPr`、`ContentTypes`、`RootRels`、`DocRels`、`Styles`、`Settings`、`Core`、`App` | 生成 docx 各 XML 部件；`Esc` 转义 `< > & "`，控制字符替换为空格 |
| `ZipWriter` | `Add(name, data)`、`Finish()`、`Crc32(data)`、`Dispose()` | 极简 ZIP 写入器：store 方式（不压缩）、UTF-8 文件名标志 `0x0800`、标准中央目录；不依赖 `System.IO.Compression` |

---

## 5. 文件 I/O 边界

| 方向 | 路径 / 来源 | 说明 |
| :--- | :--- | :--- |
| 输入 | 用户选择的图片文件 | 支持 jpg/jpeg/png/bmp/gif/tif/tiff；整文件读入内存，不锁定源文件 |
| 输入 | 嵌入资源 `AppIcon` | `Program.LoadAppIcon()` 从程序集清单读取窗口图标 |
| 输出 | 用户指定或默认目录下的 `.docx` | 目标被占用/重名时自动改名，不覆盖已有文件 |
| 临时 | 输出目录下 `.esw_<8位十六进制>.tmp` | 写入完成后删除/改名；失败时清理 |
| 外部进程 | `Process.Start(path)` / `explorer.exe /select,<path>` | 仅用户点击"现在打开检查"时启动，不涉及网络 |

---

## 结论

以上入口全部限定在本机进程内：命令行参数、WinForms 事件、本地文件读写。程序没有 HTTP 服务端、RPC 服务端、IDL 契约、网络客户端或监听端口，因此不存在可文档化为 REST/Thrift/gRPC/Dubbo 的对外接口。
