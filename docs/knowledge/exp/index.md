# 经验库

记录本仓库在需求分析、实现、验证与归档过程中沉淀的可复用经验（如构建、裁切几何、OOXML 兼容性踩坑）。

| 文件 | 主题 | 何时读 |
|------|------|--------|
| build.md | 构建与编译：csc 命令、图标双参数、C#5 语法上限、CS0016 占用、ps1 编码坑、CLI 传参引号 | 改代码后要编译、加新源文件、CLI 调用 |
| ooxml-zip.md | zip 中央目录 Offset 坑、每页独立方向的 sectPr 结构、EMU 换算、store 策略、GDI+ 流生命周期 | 改 Ooxml.cs / Zip.cs / Core.cs 排版 |
| winforms-gui.md | TableLayoutPanel 布局教训、占位文本框、无头验证 GUI 的有效手段（截图为唯一判据）、图标双通道、科目快捷键 | 改 MainForm.cs、做界面回归 |
