# OOXML / ZIP 生成经验

## EXP-Z1：zip 中央目录 Offset 必须指向 local header 起始（本仓库真实踩坑）

- `ZipWriter.Add()` 里 `Offset` 必须在**写 local header 之前**取 `_s.Position`：
  ```csharp
  uint headerOffset = (uint)_s.Position;   // ← 在写 0x04034B50 之前
  ```
- 曾经错误地记成"数据起始位置"，后果：python-docx 报 `BadZipFile: Bad magic number for file header`，WPS 打不开。
- 教训：**zip 合法性必须用真解析器验证**，肉眼/十六进制头都对不上时，用
  `python -c "import zipfile; zipfile.ZipFile(p).testzip()"` + `xml.dom.minidom` 解析每个 XML 部件。

## EXP-Z2：docx 每页独立方向靠"每段一个 sectPr + 文档级最后一个 sectPr"

- Word 分节规则：`sectPr` 放在段落 `pPr` 里＝该段结束一节；**body 末尾的 sectPr 管最后一节**。
- 因此生成 N 页需要 N 个 sectPr：前 N-1 个挂在"1 磅高占位段"上（`w:line="20" w:lineRule="exact"` + `sz=2`，几乎不占版面），最后一个放 body 级。
- 横向页：`pgSz w:w="16838" w:h="11906" w:orient="landscape"`（w=宽 h=高，**不是**交换 w/h 的写法；orient 只是提示，真正生效的是数值）。纵向 `11906×16838`。
- 混排文档（裁片竖页 + 三栏卷横页）方向可逐页独立，互不影响——已实测。
- 不设 `docGrid`，避免默认网格基线把行高撑大导致图片被挤下页。

## EXP-Z3：EMU / twip 换算与版心

- 1mm = 36000 EMU；A4 = 210×297mm = 11906×16838 twip；页边距 5mm = 283 twip。
- 版心：竖版 200×287mm，横版 287×200mm（`Metrics.TextWLandscapeEmu` 等）。
- 图片缩放预算按"版心 mm → 目标 DPI 像素"反推，`s>1` 时不放大（`MakeSlice`）。

## EXP-Z4：store（不压缩）策略是刻意的

- docx 里图片已是 JPEG，再压缩收益≈0；store 让 ZipWriter 简单可靠、CRC 校验齐全。
- 条目 flags 固定 `0x0800`（UTF-8 文件名），时间字段 DOS 格式（秒/2，年份-1980）。
- 时间戳用 `DateTime.Now` 本地时间即可，Office 不校验。

## EXP-Z5：GDI+ 图片流生命周期

- `Image.FromStream` 要求流与 Image 同生命周期，否则延迟解码时抛"内存不足"（GDI+ 的万能假错误）。
- 项目做法：`img.Tag = ms` 把 MemoryStream 挂在 Image 上防提前 GC；用完 `Dispose` Image。
- 小尺寸图防御：`Width/Height < 8` 直接判无效（BR-09），避免除零/坏图混入。
