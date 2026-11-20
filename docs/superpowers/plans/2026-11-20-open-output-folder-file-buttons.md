# 输出目录/文件快捷按钮 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 PaperToWord 主界面底部输出行增加「打开目录」和「打开文件」两个按钮，用于查看输出 docx 所在目录或直接打开该 docx。

**Architecture:** 在 `MainForm` 中提取一个统一的输出路径解析方法 `ResolveOutPath()`（与执行流程共用），两个按钮点击时分别用 `explorer.exe` 打开目录、用系统关联程序打开文件；不触碰裁切/排版/CLI 核心逻辑。

**Tech Stack:** C# 5 / .NET Framework 4.x / WinForms / BCL（零第三方依赖）。

**Spec:** 本文件即设计记录（小改动，设计经用户口头批准于 2026-11-20）。

## Global Constraints

- 编译器为系统自带 .NET Framework 4.x `csc.exe`：只用 C# 5 语法（禁用字符串插值、`nameof` 等）。
- 引用仅 `System.dll`、`System.Core.dll`、`System.Drawing.dll`、`System.Windows.Forms.dll`，不新增 NuGet。
- 构建命令：`powershell -ExecutionPolicy Bypass -File build.ps1`。
- GUI 无自动化测试框架：验证 = 编译通过 + `python tools/smoke_test.py` 不回归 + 手工清单。
- 不改动 `Source/Core.cs`、`Ooxml.cs`、`Zip.cs`、`Program.cs`、CLI 行为。

## Review Focus

- 输出框为空且列表无图片时点击按钮：应给出文字提示，不能抛异常。
- 输出框只填文件名且无图片时点击按钮：应提示无可用位置，不能拼出错误路径。
- 目标目录或 docx 不存在时：应提示而不会弹出无意义的系统错误。
- 输出框路径为相对路径时：应按当前工作目录或图片目录正确解析。
- docx 已生成但被 Word 占用时：`Process.Start` 仍应能打开（阅读场景）。

---

### Task 1: 增加「打开目录」「打开文件」按钮

**Files:**
- Modify: `Source/MainForm.cs`（字段声明区、`BuildLayout()`、`HookEvents()`、输出路径区、`RunInBackground()`）

**Interfaces:**
- Produces: `string ResolveOutPath()` — 返回输出 docx 的绝对路径；无法确定时返回 `""`。
- Produces: `void OpenOutputDir()`、`void OpenOutputFile()` — 按钮点击处理。
- Consumes: 现有 `OutPathOrEmpty()`、`SafeDir(string)`、`SuggestedOutName()`、`OpenInFolder(string)`、`BeepTip(string)`。

- [ ] **Step 1: 建立编译基线**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 输出 `OK  ...\dist\PaperToWord.exe ...`（若 exe 正在运行，先关闭窗口）。

- [ ] **Step 2: 增加两个按钮字段**

在 `readonly Button _btnOut = new Button();` 之后添加：

```csharp
readonly Button _btnOpenDir = new Button();
readonly Button _btnOpenFile = new Button();
```

- [ ] **Step 3: 在 `BuildLayout()` 中布局第一行**

在 `_btnOut.Bounds = new Rectangle(604, 12, 90, 26);` 之后添加（高度与「浏览…」一致）：

```csharp
_btnOpenDir.Text = "打开目录";
_btnOpenDir.Bounds = new Rectangle(700, 12, 90, 26);
_btnOpenFile.Text = "打开文件";
_btnOpenFile.Bounds = new Rectangle(796, 12, 90, 26);
```

并把 `_bottom.Controls.AddRange(...)` 的数组改为包含 `_btnOpenDir, _btnOpenFile`。

- [ ] **Step 4: 改写 `_bottom.Resize` 的宽度计算**

将现有 `_bottom.Resize` 委托替换为（三个按钮从右向左排，输出框自适应）：

```csharp
_bottom.Resize += delegate
{
    const int bw = 90, gap = 6;
    int x = Math.Max(92, _bottom.ClientRectangle.Width - 8 - bw);
    _btnOpenFile.Left = x; x -= bw + gap;
    _btnOpenDir.Left = x; x -= bw + gap;
    _btnOut.Left = x;
    _outBox.Width = Math.Max(160, _btnOut.Left - 92 - 8);
};
```

- [ ] **Step 5: 绑定点击事件**

在 `HookEvents()` 的 `_btnOut.Click += ...` 之后添加：

```csharp
_btnOpenDir.Click += delegate { OpenOutputDir(); };
_btnOpenFile.Click += delegate { OpenOutputFile(); };
_tip.SetToolTip(_btnOpenDir, "在资源管理器中打开输出 docx 所在目录");
_tip.SetToolTip(_btnOpenFile, "用默认程序（Word/WPS）打开输出 docx");
```

- [ ] **Step 6: 提取 `ResolveOutPath()` 并让执行流程复用**

在 `SuggestedOutName()` 之后添加：

```csharp
// 解析最终输出 docx 路径；无法确定（无图片且未填路径）时返回空串。
string ResolveOutPath()
{
    string t = OutPathOrEmpty();
    if (t.Length == 0)
    {
        if (_files.Count == 0) return "";
        return Path.Combine(SafeDir(_files[0]), SuggestedOutName());
    }
    if (Path.GetDirectoryName(t.TrimEnd('\\', '/')).Length == 0)
    {
        if (_files.Count == 0) return "";
        t = Path.Combine(SafeDir(_files[0]), t);
    }
    if (!t.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) t += ".docx";
    return t;
}
```

`RunInBackground()` 中把原四行解析逻辑替换为：

```csharp
string outPath = ResolveOutPath();
```

（该处此前已保证 `_files.Count > 0`，行为不变。）

- [ ] **Step 7: 实现两个点击处理**

在 `OpenInFolder(string path)` 之前添加：

```csharp
void OpenOutputDir()
{
    string path = ResolveOutPath();
    if (path.Length == 0) { BeepTip("还没有可用的输出目录：请先添加图片或填写输出路径。"); return; }
    string dir = Path.GetDirectoryName(path);
    try
    {
        if (!Directory.Exists(dir)) { BeepTip("目录不存在：" + dir); return; }
        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("explorer.exe");
        psi.Arguments = "\"" + dir + "\"";
        System.Diagnostics.Process.Start(psi);
    }
    catch (Exception ex) { BeepTip("打开目录失败：" + ex.Message); }
}

void OpenOutputFile()
{
    string path = ResolveOutPath();
    if (path.Length == 0) { BeepTip("还没有可打开的文件：请先添加图片或填写输出路径。"); return; }
    if (!File.Exists(path))
    {
        BeepTip("docx 还没生成：" + path + "。请先点「执行」生成。");
        return;
    }
    try { System.Diagnostics.Process.Start(path); }
    catch (Exception ex) { BeepTip("打开文件失败：" + ex.Message); OpenInFolder(path); }
}
```

- [ ] **Step 8: 编译验证**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 输出 `OK  ...\dist\PaperToWord.exe ...`，无编译错误。

- [ ] **Step 9: 冒烟测试回归**

Run: `python tools/smoke_test.py`
Expected: 退出码 0（CLI 行为未变）。

- [ ] **Step 10: 手工验证清单（启动 GUI）**

1. 空列表点「打开目录」/「打开文件」→ 日志出现提示，不崩溃。
2. 添加图片、输出框留空：点「打开目录」→ 打开第一张图所在目录；点「打开文件」→ 提示 docx 未生成。
3. 执行生成后：点「打开目录」→ 打开输出目录；点「打开文件」→ Word/WPS 打开生成的 docx。
4. 拖动窗口改变宽度 → 三个按钮与输出框不重叠、输出框不小于 160px。

- [ ] **Step 11: 提交**

```bash
git add Source/MainForm.cs docs/superpowers/plans/2026-11-20-open-output-folder-file-buttons.md
git commit -m "GUI 增加打开输出目录/文件按钮"
```

---

## 执行记录（2026-11-20）

**已完成：** Step 1–9。编译验证通过（`csc` exit 0），`tools/smoke_test.py` 对新产物 PASS。
**待人工确认：** Step 10 的 GUI 手工清单（需要用户启动窗口操作）。
**未执行：** Step 11 提交（等待用户授权）。

### 与计划的偏差（Ruling）

1. `ResolveOutPath` 整体包在 `try/catch` 中，路径非法时返回 `""`。原因：独立评审发现输出框含 `|` 等非法字符时 `Path.Combine` 会在按钮回调抛未处理异常。代价：极少数非法输入从“解析异常”变为“提示输出位置不可用”，行为更安全。
2. `ResolveOutPath` 返回的是“路径”而非严格“绝对路径”：输入 `sub\a.docx` 时原样返回，各调用方均按当前工作目录解析，功能一致。代价：文档措辞与实现略有出入，已在此记录。
3. 根路径输入（`C:` / `C:\`）的行为与改动前不同：旧代码 NRE/抛异常，新代码解析为 `C:.docx` / `C:\.docx` 后交由写盘逻辑报错。代价：错误信息由“解析异常”变为“写入失败”，更可读。
4. 评审提出的三项 Minor 未修改，理由：窗口 `MinimumSize=960` 下窄宽度分支不可达；`Process` 未 `Dispose` 与既有 `DoneDialog`/`OpenInFolder` 写法一致；「打开文件」在生成前可能打开同名旧文件，属于设计内行为（打开当前存在的目标文件）。

