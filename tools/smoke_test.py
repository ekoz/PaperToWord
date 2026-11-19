# -*- coding: utf-8 -*-
"""PaperToWord 冒烟测试（仅标准库，CI 与本地通用）。

流程：
  1. 用 zlib 手写一张横版"试卷"PNG（1400x1000，纯标准库，无需 Pillow）
  2. 调用 PaperToWord.exe 命令行模式生成 docx
  3. 校验 docx：zip 完整性 + 必需部件 + 每个 XML 可解析 + 图片数量 + 分节数量

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
    print("PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
