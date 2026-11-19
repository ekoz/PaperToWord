# 数据模型清单

> 生成时间：2026-11-19
> 数据表总数：0 张

## 扫描结论

本服务无直连数据库（未识别到任何持久化证据）。

项目形态：C# / .NET Framework 4.x WinForms 桌面工具（`PaperToWord`，试卷裁页并生成 docx），
仅依赖 BCL，单文件 exe 绿色运行，无服务端、无数据库、无 ORM、无持久化 schema。

已验证为空的证据清单：

- 无 ORM 映射：`Source/` 下全部 6 个 `.cs` 文件中不含 `@Entity` / `@Table` / `@TableName` /
  GORM Model / JPA Repository 等映射声明。
- 无 MyBatis：无 `@Mapper` / `<resultMap>` / Mapper XML（仓库内不存在 `.xml` 映射文件）。
- 无 DDL / SQL 文件：仓库根目录及两级子目录均无 `sql/`、`migrations/`、`schema/`、`db/` 目录，
  亦无任何 `.sql` 文件。
- 无数据源配置：无 `app.config` / `web.config` / `settings.json` / `.yaml` 等配置文件，
  未出现 `spring.datasource`、JDBC URL、HikariCP、Druid、`@DS`、`ConnectionString` 等配置。
- 无数据库访问依赖：`Source/` 内未出现 `System.Data`、`SqlConnection`、`SqlCommand`、`OleDb`、
  `EntityFramework` 等符号；`Program.cs` 明确声明"依赖：仅 .NET Framework 4.x 自带 BCL，无第三方库"。
- 无数据访问代码路径：文件读写仅针对输入图片、输出 `.docx`、临时文件与内存流
  （`Core.cs` 的 `ImageUtil.Load`、`DocxBuilder.Write`、`MediaStore`，`Zip.cs` 的 ZIP 写出）。
- 仓库根目录 `Glob` 结果仅含 `Source/`、`dist/`、`.git/`、`.helixharness/`、`docs/`，
  无任何数据库工程、迁移或 schema 资产。

> 结论：本服务不直接持有数据表。仓库内不存在任何持久化证据，表清单与 ER 图均为空。

## 表清单总览

（无）

## 表字段详情

（无）

## 附录：非数据库数据结构（非表）

> 以下结构体仅存在于进程内存或输出文件（`.docx`）中，用于描述裁切参数、内存裁片、
> 页面排版与 OOXML 包部件。它们**不是数据库表**，无表名、无主键、无索引、无生命周期持久化，
> 仅作为理解业务数据形态的参考。

### A.1 配置结构体 `Config`（`Source/Core.cs:13-22`）

| 字段名 | 类型 | 默认值 | 含义 | 枚举值说明 |
| :--- | :--- | :--- | :--- | :--- |
| Mode | SplitMode | Auto | 裁切方式 | `0:Auto 自动`、`1:LR 强制左右裁`、`2:TB 强制上下裁`、`3:None 不裁切` |
| Position | double | 50.0 | 裁切线位置（百分比） | - |
| Quality | int | 300 | 目标 DPI；0 表示不降采样（保留原图） | - |
| TwoUp | bool | false | 两片是否上下排在同一页 | - |
| FillRatio | double | 0.96 | 单片占版心比例 | - |
| MaxSourceSide | int | 4600 | 输入长边降采样上限；0 表示不降 | - |
| AutoLandscape | bool | true | 不裁切的横图是否自动改用横向 A4 | - |

> 枚举 `SplitMode` 来源：`Source/Core.cs:11`。
> 界面下拉项到枚举的映射见 `Source/MainForm.cs:540`（`CurrentConfig`）。

### A.2 内存裁片 `Slice`（`Source/Core.cs:24-28`）

| 字段名 | 类型 | 含义 |
| :--- | :--- | :--- |
| W | int | 裁片像素宽 |
| H | int | 裁片像素高 |
| Jpeg | byte[] | 编码后的 JPEG 字节 |

### A.3 输出页 `Page`（`Source/Core.cs:31-38`）

| 字段名 | 类型 | 含义 |
| :--- | :--- | :--- |
| RelIds | List&lt;string&gt; | 该页图片的 OOXML 关系 ID（`rIdN`） |
| EmuW | List&lt;long&gt; | 各图片显示宽度（EMU） |
| EmuH | List&lt;long&gt; | 各图片显示高度（EMU） |
| Names | List&lt;string&gt; | 各图片名称（写入 docPr/cNvPr） |
| Landscape | bool | 该页是否使用横向 A4（297×210mm） |

### A.4 内存媒体暂存 `MediaStore`（`Source/Core.cs:335-346`）

进程内 `Dictionary<string, byte[]>`，键为 `media/imageN.jpg` 包内路径，值为 JPEG 字节；
写入 docx 后调用 `Clear()` 释放，不做任何持久化。

### A.5 输出文档（OOXML 包）部件结构（`Source/Core.cs:363-372`）

一次运行产出一个 `.docx`（ZIP，store 存储，见 `Source/Zip.cs`）文件，包内部件如下：

| 包内部件路径 | 内容 | 生成方法 |
| :--- | :--- | :--- |
| `[Content_Types].xml` | 内容类型声明 | `Ooxml.ContentTypes()` |
| `_rels/.rels` | 包级关系 | `Ooxml.RootRels()` |
| `docProps/core.xml` | 文档核心属性（标题、创建者、时间） | `Ooxml.Core()` |
| `docProps/app.xml` | 扩展属性（应用程序名、版本） | `Ooxml.App()` |
| `word/styles.xml` | 样式（字体、字号、段落默认值） | `Ooxml.Styles()` |
| `word/settings.xml` | 文档设置（缩放、兼容模式） | `Ooxml.Settings()` |
| `word/document.xml` | 正文，一页一节（裁片独占 A4） | `Ooxml.Document(pages)` |
| `word/_rels/document.xml.rels` | 文档级关系（图片、样式、设置） | `Ooxml.DocRels(pages)` |
| `word/media/imageN.jpg` | 裁片图片，N 从 1 递增 | `Sizer.AddPiece` → `MediaStore.Put` |

> 该包结构是文件格式层面的产物模型，与数据库表无关；页面尺寸常量见
> `Source/Core.cs:40-55`（`Metrics`，A4 210×297mm、页边距 5mm、EMU 换算 36000）。
