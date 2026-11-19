# 构建与编译经验

## EXP-B1：构建只能用 Windows 自带的 csc.exe（可复现命令见根目录 `build.ps1`）

- 完整命令已固化到仓库根目录 **`build.ps1`**，直接 `powershell -ExecutionPolicy Bypass -File build.ps1`。
- 编译器路径：`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（64 位优先，缺失回退 Framework 32 位）。
- 关键参数缺一不可：
  - `/target:winexe`（无控制台窗口；副作用是 CLI 模式必须用 `Start-Process -Wait` 调用）
  - `/win32icon:Source\app.ico` **与** `/resource:Source\app.ico,AppIcon` 必须**同时**给：前者只设 PE 图标（资源管理器/快捷方式图标），后者才让运行时 `GetManifestResourceStream("AppIcon")` 读到清单资源（窗口/任务栏图标）。只给其一必有一半图标失效。
  - 引用只有 BCL 四件套：System / System.Core / System.Drawing / System.Windows.Forms。
- **C# 5 语法上限**：系统 csc 是 .NET 4.x 自带老编译器，不能用字符串插值 `$""`、`nameof`、表达式体成员、`?.` 等 C#6+ 语法；Lambda 参数名注意与外层局部变量重名（会报 CS0136）。
- 新增 `.cs` 文件必须同步加进 `build.ps1` 的 `$files` 清单，否则静默不参与编译。

## EXP-B2：CS0016「未能写入输出文件」＝ exe 正在运行

- 报错文本是 GBK 乱码（"δдļ"），真实含义是目标 exe 被占用——用户开着 PaperToWord 窗口时编译必失败。
- `build.ps1` 已内置占用预检（试开 `FileShare.None`），失败时给出中文提示"请先关闭正在运行的窗口"。
- 排查命令：`Get-Process -Name PaperToWord`。

## EXP-B3：PowerShell 脚本编码坑（本仓库已踩过两次）

- Windows PowerShell 5.1 把**无 BOM 的 UTF-8** `.ps1` 按 GBK 解析 → 中文注释/字符串导致 ParserError（引号被吃掉）。`.ps1` 含中文时必须存成 **UTF-8 with BOM**。
- 反过来，`.cs` 源码必须 **UTF-8 无 BOM**（csc 两者都能编，但 diff 稳定用无 BOM）；docx 内 XML 部件必须 **BOM-less UTF-8**（`new UTF8Encoding(false)`，见 `Ooxml.Utf8()`）。
- 注意：`Encoding.UTF8.GetBytes(string)` **从不输出 BOM**（BOM 只在 GetPreamble/写流重载里），别把它当违规——`Zip.cs` 用它编码条目文件名是正确用法。

## EXP-B4：CLI 参数传递（路径含空格）

- winexe 无法直接看到控制台输出；PowerShell 调用要 `Start-Process -FilePath $exe -ArgumentList '<整串预引号参数>' -Wait`。
- 图片路径含空格/中文时，必须在 ArgumentList 字符串里**手工加双引号**（`'"' + $path + '"'`），用 `-ArgumentList @($a,$b)` 数组形式传会丢引号、参数被拆散，程序会误入 GUI 模式。
- `--out=` 支持两种形态；只给文件名时自动落到第一张图所在目录（GUI/CLI 同规则）。
