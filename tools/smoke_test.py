# -*- coding: utf-8 -*-
"""PaperToWord 冒烟测试（仅标准库，CI 与本地通用）。

流程：
  1. 用 zlib 手写一张横版"试卷"PNG（1400x1000，纯标准库，无需 Pillow）
  2. 调用 PaperToWord.exe 命令行模式生成 docx
  3. 校验 docx：zip 完整性 + 必需部件 + 每个 XML 可解析 + 图片数量 + 分节数量
  4. 回归 A：全部图片无效 → 退出码 1 且不产出任何 docx
  5. 回归 B：目标重名 → 自动改名 _2，绝不覆盖第一个文件，stdout 报告实际路径

退出码 0 = 通过；非 0 = 失败（打印原因）。
用法：python tools/smoke_test.py [exe 路径]
"""
import os
import struct
import subprocess
import sys
import tempfile
import zipfile
import zlib
from xml.dom import minidom

REQUIRED_PARTS = [
    "[Content_Types].xml",
    "_rels/.rels",
    "word/document.xml",
    "word/_rels/document.xml.rels",
    "docProps/core.xml",
    "docProps/app.xml",
]


def make_png(path, w=1400, h=1000):
    """生成横版白底带黑色边框与几条"题目"横线的 PNG（模拟试卷，可被裁切）。"""
    black = b"\x20\x20\x20"
    white = b"\xff\xff\xff"
    rows = []
    for y in range(h):
        edge = y < 8 or y >= h - 8
        content = (40 <= y < 46) or (200 <= y < 900 and (y - 200) % 120 < 6)
        if edge:
            row = bytearray(black * w)
        elif content:
            row = bytearray(black * w)
            row[60 * 3:(w - 60) * 3] = black * (w - 120)
            row[:8 * 3] = black * 8
            row[(w - 8) * 3:] = black * 8
            row[8 * 3:60 * 3] = white * 52
            row[(w - 60) * 3:(w - 8) * 3] = white * 52
        else:
            row = bytearray(white * w)
            row[:8 * 3] = black * 8
            row[(w - 8) * 3:] = black * 8
        rows.append(b"\x00" + bytes(row))          # filter type 0
    co = zlib.compressobj(9, zlib.DEFLATED, -15)
    body = b"".join(co.compress(r) for r in rows) + co.flush()

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
           + chunk(b"IDAT", body)
           + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)
    return w, h


def fail(msg):
    print("FAIL: " + msg)
    return 1


def run_exe(exe, args):
    """命令行模式调用；返回 (退出码, stdout)。winexe 的输出靠继承的管道句柄捕获。"""
    r = subprocess.run([exe] + list(args), timeout=300,
                       capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    out = (r.stdout or "").strip()
    print("stdout: " + out)
    return r.returncode, out


def check_all_fail(exe, tmp):
    """回归 A：全部图片处理失败 → 退出码 1，且不产出空 docx。"""
    bad = os.path.join(tmp, "bad.png")
    with open(bad, "wb") as f:
        f.write(b"not a png at all")          # 扩展名合法，解码必失败
    out = os.path.join(tmp, "regA", "a.docx")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    rc, _ = run_exe(exe, [bad, "--out=" + out])
    if rc != 1:
        return fail("回归A 全部失败时退出码应为 1，实际 %d" % rc)
    made = [n for n in os.listdir(os.path.dirname(out)) if n.endswith(".docx")]
    if made:
        return fail("回归A 全部失败时不应产出 docx，实际有: " + ", ".join(made))
    print("regA  : 全部失败 → 退出码 1，无空文档")
    return 0


def check_rename(exe, tmp):
    """回归 B：目标重名 → 自动改名 _2、绝不覆盖第一个文件；stdout 报告实际路径。"""
    img = os.path.join(tmp, "paper_rename.png")
    make_png(img)
    d = os.path.join(tmp, "regB")
    os.makedirs(d, exist_ok=True)
    out = os.path.join(d, "b.docx")

    rc, _ = run_exe(exe, [img, "--out=" + out])
    if rc != 0 or not os.path.isfile(out):
        return fail("回归B 第一次生成失败（退出码 %d）" % rc)
    first = open(out, "rb").read()

    rc, text = run_exe(exe, [img, "--out=" + out])
    if rc != 0:
        return fail("回归B 第二次生成失败（退出码 %d）" % rc)
    second = os.path.join(d, "b_2.docx")
    if not os.path.isfile(second):
        return fail("回归B 重名时未自动改名生成 b_2.docx")
    if open(out, "rb").read() != first:
        return fail("回归B 第一个文件被覆盖（违反绝不覆盖约束）")
    if "b_2.docx" not in text:
        return fail("回归B stdout 未报告改名后的实际路径，实际输出: " + text)
    print("regB  : 重名 → b.docx 未被覆盖，另存 b_2.docx 且 stdout 报告实际路径")
    return 0


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    exe = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, "dist", "PaperToWord.exe")
    if not os.path.isfile(exe):
        return fail("找不到 exe: " + exe)

    tmp = tempfile.mkdtemp(prefix="ptw_smoke_")
    img = os.path.join(tmp, "paper1.png")
    make_png(img)
    out = os.path.join(tmp, "smoke.docx")

    print("exe   : " + exe)
    print("image : " + img)
    r = subprocess.run([exe, img, "--out=" + out], timeout=300,
                       capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    print("stdout: " + (r.stdout or "").strip())
    if r.returncode != 0:
        return fail("exe 退出码 %d" % r.returncode)
    if not os.path.isfile(out):
        return fail("未生成 docx: " + out)

    size = os.path.getsize(out)
    if size < 1024:
        return fail("docx 过小(%d bytes)，疑似空文档" % size)

    z = zipfile.ZipFile(out)
    bad = z.testzip()
    if bad is not None:
        return fail("zip CRC 校验失败: " + bad)
    names = set(z.namelist())
    missing = [p for p in REQUIRED_PARTS if p not in names]
    if missing:
        return fail("缺少部件: " + ", ".join(missing))
    for n in names:
        if n.endswith(".xml") or n.endswith(".rels"):
            try:
                minidom.parseString(z.read(n))
            except Exception as e:
                return fail("XML 解析失败 %s: %s" % (n, e))
    media = [n for n in names if n.startswith("word/media/")]
    if not media:
        return fail("无内嵌图片")
    doc = z.read("word/document.xml").decode("utf-8")
    sections = doc.count("<w:sectPr")
    if sections < 1:
        return fail("无分节符 sectPr")
    z.close()

    print("docx  : %d bytes, media=%d, sections=%d, xml parts OK" % (size, len(media), sections))

    rc = check_all_fail(exe, tmp)
    if rc:
        return rc
    rc = check_rename(exe, tmp)
    if rc:
        return rc

    print("PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
