#!/usr/bin/env python3
"""PaperToWord 基础不变量检查（Python 3.10+，仅标准库）。

检查项（来源：docs/harness/architecture/index.md 中已确认的架构约束）：
1. 零第三方依赖：不得出现 NuGet/Paket 清单或 PackageReference。
2. 源码 using 只允许 System.* 与项目自身命名空间。
3. 网络 / 数据库 / MQ 引入告警（架构声明为无相关能力）。
4. OOXML XML 输出使用 BOM-less UTF-8 告警。

退出码：0 = 通过（或仅告警）；1 = 存在硬性违规。
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "Source"


def iter_source_files() -> list[Path]:
    if not SOURCE.is_dir():
        return []
    return sorted(SOURCE.rglob("*.cs"))


def collect_project_namespaces(files: list[Path]) -> set[str]:
    namespaces: set[str] = set()
    pattern = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)", re.MULTILINE)
    for path in files:
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        namespaces.update(pattern.findall(text))
    return namespaces


def check_dependency_manifests() -> list[str]:
    violations: list[str] = []
    for name in ("packages.config", "paket.dependencies", "Directory.Packages.props"):
        if (ROOT / name).is_file():
            violations.append(f"发现第三方依赖清单：{name}")
    for project in ROOT.glob("**/*.csproj"):
        try:
            text = project.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        if "PackageReference" in text:
            violations.append(f"{project.relative_to(ROOT)} 包含 PackageReference")
    return violations


def check_usings(files: list[Path], project_namespaces: set[str]) -> list[str]:
    violations: list[str] = []
    pattern = re.compile(r"^\s*using\s+(?:static\s+)?([A-Za-z_][\w.]*)\s*;", re.MULTILINE)
    for path in files:
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for namespace in pattern.findall(text):
            allowed = namespace == "System" or namespace.startswith("System.")
            allowed = allowed or namespace in project_namespaces
            allowed = allowed or any(
                namespace.startswith(ns + ".") for ns in project_namespaces
            )
            if not allowed:
                violations.append(
                    f"{path.relative_to(ROOT)}: using {namespace};（非 BCL/项目命名空间）"
                )
    return violations


def check_expansion_risks(files: list[Path]) -> list[str]:
    warnings: list[str] = []
    risky = (
        (re.compile(r"\bHttpClient\b|\bSystem\.Net\b|\bSocket\b"), "网络通信"),
        (re.compile(r"\bSqlConnection\b|\bSystem\.Data\b"), "数据库访问"),
        (re.compile(r"\bKafka\b|\bRabbitMQ\b|\bRocketMQ\b"), "消息队列"),
    )
    for path in files:
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for pattern, label in risky:
            if pattern.search(text):
                warnings.append(f"{path.relative_to(ROOT)}: 出现疑似{label}符号")
    return warnings


def check_utf8_bom_risk(files: list[Path]) -> list[str]:
    """OOXML 部件必须是 BOM-less UTF-8。

    注意：Encoding.UTF8.GetBytes(string) 本身不输出 BOM（BOM 只来自 GetPreamble），
    因此不把它当违规。真正会写出 BOM 的是走 preamble 的写盘/写流重载：
      - File.WriteAllText / File.AppendAllText 传 Encoding.UTF8
      - new StreamWriter(stream, Encoding.UTF8)（未用 UTF8Encoding(false)）
      - new UTF8Encoding(true)
    项目正确做法是 Ooxml.Utf8()（内部 new UTF8Encoding(false)）。
    """
    warnings: list[str] = []
    risky = (
        (re.compile(r"File\.(WriteAllText|AppendAllText)\s*\([^)]*Encoding\.UTF8\b"),
         "File.WriteAllText/AppendAllText 传 Encoding.UTF8 会写出 BOM"),
        (re.compile(r"new\s+StreamWriter\s*\([^)]*Encoding\.UTF8\b"),
         "new StreamWriter(..., Encoding.UTF8) 会写出 BOM"),
        (re.compile(r"new\s+UTF8Encoding\s*\(\s*true"),
         "new UTF8Encoding(true) 显式带 BOM"),
    )
    for path in files:
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for pattern, why in risky:
            for m in pattern.finditer(text):
                line = text[:m.start()].count("\n") + 1
                warnings.append(
                    f"{path.relative_to(ROOT)}:{line}: {why}；"
                    "OOXML 部件应使用 BOM-less UTF-8（见 Ooxml.Utf8）"
                )
    return warnings


def main() -> int:
    print("=== HelixHarness invariant check (PaperToWord) ===")
    files = iter_source_files()
    if not files:
        print("未找到 Source/*.cs，跳过源码检查（仓库结构可能已变化）")
        return 0

    project_namespaces = collect_project_namespaces(files)
    violations = check_dependency_manifests()
    violations.extend(check_usings(files, project_namespaces))
    warnings = check_expansion_risks(files)
    warnings.extend(check_utf8_bom_risk(files))

    for item in warnings:
        print(f"WARNING {item}")
    for item in violations:
        print(f"VIOLATION {item}")

    if violations:
        print(f"=== 失败：{len(violations)} 项硬性违规 ===")
        return 1
    print("=== 通过（告警 %d 项）===" % len(warnings))
    return 0


if __name__ == "__main__":
    sys.exit(main())
