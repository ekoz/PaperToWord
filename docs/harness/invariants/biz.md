# 业务不变量

> 规范性来源说明：本文件遵循 `reference/harness-generation-protocol.md`。扫描结果只能证明
> **当前行为**，不能单独证明该行为必须永远不变。下列条目已区分「已确认约束」与「当前行为参考」。

## 已确认的跨需求业务不变量

**暂无。** 当前没有来自仓库既有规约或用户明确确认的长期业务规则。后续需求若需要固定某条业务规则，
应在 Spec 中确认并回写本文件，同时注明确认来源。

## 当前行为参考（不得自动升级为硬规则）

以下规则来自 `docs/knowledge/domain-model.md` 对现有 v1.3 实现的扫描，描述**当前**行为，
用于理解兼容面。任何一条都可能在后续已确认 Spec 中演进；变更时应在 Spec 中写明影响范围与迁移方式。

| 编号 | 当前行为 | 来源 |
|------|---------|------|
| BR-01 | 只接受 jpg / jpeg / png / bmp / gif / tif / tiff 图片 | domain-model.md |
| BR-02 | 小于 8×8 像素的图片判为无效并跳过 | domain-model.md |
| BR-03 | 同一图片按完整路径、不区分大小写去重 | domain-model.md |
| BR-04 | "自动"模式仅对横图（宽 > 高）沿竖中线左右裁切，竖图不裁 | domain-model.md |
| BR-05 | 裁切位置百分比业务上限 20%–80%（界面滑杆 30%–70%） | domain-model.md |
| BR-06 | 仅当"两片同页"开启且实际裁出两片时才同页排布 | domain-model.md |
| BR-07 | 未裁切横图在"自动横向 A4"开启时该页改用横向 A4 | domain-model.md |
| BR-08 | 目标 docx 已存在时自动改名（`_2`…`_199`，仍冲突追加时分秒），绝不覆盖 | domain-model.md |
| BR-09 | 单图失败不中断整批；成功页数为 0 时才判定整批失败 | domain-model.md |
| BR-10 | 输出路径缺省时落到首图目录并命名 `yyyyMMdd-.docx` | domain-model.md |
| BR-11 | 同一时刻只允许一个生成任务 | domain-model.md |
| BR-12 | 所有来源统一转为白底 JPEG（透明填白，GIF 取首帧） | domain-model.md |
| BR-13 | 默认 300 DPI；"原图"档不降采样且取消 4600px 长边限制 | domain-model.md |
| BR-14 | 文件名自然排序，连续数字段按数值比较 | domain-model.md |
| BR-15 | 命令行模式固定使用默认参数，不解析裁切/清晰度/排布选项 | domain-model.md |
| BR-16 | EXIF 方向标记自动识别并旋转后再裁切 | domain-model.md |

## 待确认

- 上述规则中哪些属于必须长期保持的产品承诺（尤其 BR-08 不覆盖、BR-09 部分失败继续、BR-12 统一 JPEG）。<!-- TODO: 待确认 -->
- 使用者群体与产品定位（教师/家长/其他）。<!-- TODO: 待确认 -->
