# WinForms GUI 经验

## EXP-G1：选项区布局必须用 TableLayoutPanel，禁止手算像素

- 右侧面板宽度随窗口变（默认 1080 窗口时仅 ~620–700px，SplitContainer Panel1=330 固定）。
- 早期用绝对坐标手算布局，两次被用户截图打回：①"工作方式"说明行被滑杆/提示文字压住；②"页面排布"下拉整个被挤出面板。
- 结构化解法（现行代码 `BuildOptions()`）：6 列（AutoSize 标签 + Percent 下拉 ×3）、4 行（30px 下拉行 / 34px 滑杆行 / 26px 复选框行 / Percent 说明行），说明行独占整行 `SetColumnSpan(6)`——**任何宽度下不可能被别的控件叠住**。
- 下拉选项文字要短（"自动：横版裁"），完整描述放 `ToolTip`；标签 `AutoEllipsis = true` 兜底。
- 验证方法：最小宽 960 / 默认 1080 / 宽 1500 三档截图检查（PrintWindow 截图，见 EXP-G3）。

## EXP-G2：占位符文本框（placeholder）的两个坑

- `_outBox` 用灰色占位文案 + GotFocus/LostFocus 切换。读取时**必须把等于占位文案的文本视为空**（`OutPathOrEmpty()`），否则占位串会被当路径。
- 只填文件名（无目录）时，`Path.GetDirectoryName(t)` 为空串 → 自动拼到第一张图所在目录；GUI 与 CLI 同规则。

## EXP-G3：无头验证 GUI 的手段（本会话反复调试的结论）

- 截图：`PrintWindow(hwnd, hdc, 2)`（PW_RENDERFULLCONTENT）对 WinForms 有效，能拍到被遮挡窗口。
- 枚举控件：类名前缀 `WindowsForms10.BUTTON.*` / `WindowsForms10.EDIT.*`，`EnumChildWindows` + `GetWindowText` 匹配中文按钮文字。
- **点击 WinForms 按钮**：`PostMessage BM_CLICK` 和裸 `SendMessage BM_CLICK` 都**不触发 Click 事件**（无输出、无异常，极难排查）。有效序列：
  `SetForegroundWindow` → `WM_MOUSEACTIVATE(0x0020)=MA_ACTIVATE` → `WM_LBUTTONDOWN(0x0201, wParam=MK_LBUTTON, lParam=客户区中心点)` → `WM_LBUTTONUP(0x0202)`。
- UIA（UIAutomationClient）按标题匹配会因标题里的全角破折号"—"失败；按 `ProcessIdProperty` 匹配可行，但 WinForms 子控件在 UIA 树里可能枚举不到（返回 0 个按钮）——别依赖它。
- 读 Edit 内容：`GetWindowText` 拿到的是占位文案，不能区分"点击无效"和"读取无效"——最终以**截图**为唯一可信判据。

## EXP-G4：exe 图标双通道

- `/win32icon:app.ico` → 资源管理器/快捷方式图标（PE 资源）。
- `/resource:app.ico,AppIcon` → 清单资源，`LoadAppIcon()` 用 `new Icon(stream, 32, 32)` 设给 `Form.Icon`（任务栏/标题栏）。两者必须同时给（详见 EXP-B1）。
- 多尺寸 ICO（16–256，PNG 帧）由 `crop.png` 用 Pillow 预生成一次，入库 `Source\app.ico`；终端用户机器无 Python。
- 窗口图标验证：`WM_GETICON` 的 `wParam=1`(ICON_BIG)/`0`(ICON_SMALL)，传其它值返回 0 是假阴性。

## EXP-G5：科目快捷键按钮

- 底部"执行"按钮右侧三个小按钮：数学/语文/英语 → `FillSubjectName(key)` 填 `yyyyMMdd-<key>.docx`。
- 若输出框已是完整路径，只替换文件名部分、保留目录（`Path.GetDirectoryName` 判断）。
- 新增科目只需在 `subjects` 二维数组加一行；key 用拼音/英文小写，避免中文文件名兼容问题。
