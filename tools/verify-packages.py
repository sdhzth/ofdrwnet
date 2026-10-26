#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""发包门禁：校验打包产物目录的包数量、版本一致性与符号包配对。

背景：v1.3.0 发版时出现过「某个 csproj 硬编码了旧的 <Version>，产出
NetOfd.Core.1.2.0.nupkg」而发布脚本去找 1.3.0 的包 → 缺包失败。
本脚本把这类问题挡在**换取 NuGet 临时 Key 之前**，避免浪费一次性凭据。

用法：
    python tools/verify-packages.py artifacts/nupkgs 1.3.1
    python tools/verify-packages.py artifacts/nupkgs 1.3.1 --expect-count 14

退出码 0 = 门禁通过；非 0 = 门禁未通过（CI 应当中止发布）。

⚠️ **输出文案一律使用纯 ASCII**。
   GitHub `windows-latest` runner 上 Python 的 stdout 默认编码是 cp1252（ANSI 代码页），
   打印中文或框线字符会抛 UnicodeEncodeError 直接把门禁脚本打崩（已实测踩过：
   报错行号曾指向本文件的 print 行）。因此：固定文案用英文，
   外部数据（目录路径等）由下方的 UTF-8 reconfigure + errors="replace" 兜底。
"""

from __future__ import annotations

import argparse
import re
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree

# 发布拓扑顺序（与 tools/publish-nuget.sh 保持一致）
EXPECTED_PACKAGES = [
    "NetOfd.Core",
    "NetOfd.Font",
    "NetOfd.Pkg",
    "NetOfd.Reader",
    "NetOfd.Sign",
    "NetOfd.Crypto",
    "NetOfd.Archive",
    "NetOfd.Layout",
    "NetOfd.Graphics2D",
    "NetOfd.Converter",
    "NetOfd.Emr",
    "NetOfd.Tool",
    "NetOfd",
    "NetOfd.Full",
]

# 包文件名形如 NetOfd.Core.1.3.1.nupkg
# 版本段必须严格按 SemVer 三段式匹配：包 ID 本身含点号（NetOfd.Core），
# 若版本段允许任意字符会被贪婪拆成 id="NetOfd.Core.1.3" / ver="1"（实测踩过）。
NUPKG_RE = re.compile(
    r"^(?P<base>.+)\.(?P<ver>\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.\-]+)?)\.nupkg$"
)


def read_nuspec(pkg: Path) -> tuple[str, str] | None:
    """读取 nupkg 内 nuspec 的 (id, version)；读不到返回 None。"""
    try:
        with zipfile.ZipFile(pkg) as zf:
            names = [n for n in zf.namelist() if n.lower().endswith(".nuspec")]
            if not names:
                return None
            root = ElementTree.fromstring(zf.read(names[0]))
    except (zipfile.BadZipFile, ElementTree.ParseError, OSError):
        return None

    def find(tag: str) -> str:
        # nuspec 带默认命名空间，按本地名匹配
        for el in root.iter():
            if el.tag.rsplit("}", 1)[-1] == tag:
                return (el.text or "").strip()
        return ""

    return find("id"), find("version")


def main() -> int:
    # 外部数据（路径等）可能含非 ASCII：强制 UTF-8 输出并以 replace 兜底，
    # 避免 Windows runner 的 cp1252 stdout 因一个字符直接崩掉整个门禁。
    for _stream in (sys.stdout, sys.stderr):
        try:
            if hasattr(_stream, "reconfigure"):
                _stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:  # pragma: no cover - 极端环境兜底
            pass

    ap = argparse.ArgumentParser(
        description="Verify release artifacts: package count, version consistency, symbol pairing"
    )
    ap.add_argument("dir", help="packaged output directory, e.g. artifacts/nupkgs")
    ap.add_argument("version", help="expected version, e.g. 1.3.1")
    ap.add_argument("--expect-count", type=int, default=len(EXPECTED_PACKAGES),
                    help="expected number of nupkg files (default: %(default)s)")
    args = ap.parse_args()

    root = Path(args.dir)
    ver = args.version
    errors: list[str] = []

    if not root.is_dir():
        print(f"[FAIL] output directory not found: {root}")
        return 1

    nupkgs = sorted(p for p in root.glob("*.nupkg"))
    snupkgs = sorted(p for p in root.glob("*.snupkg"))

    if not nupkgs:
        print(f"[FAIL] no .nupkg files found in {root}")
        return 1

    # 1) every expected package must be present with the expected version
    names = {p.name for p in nupkgs}
    for pid in EXPECTED_PACKAGES:
        want = f"{pid}.{ver}.nupkg"
        if want not in names:
            errors.append(f"missing package: {want}")

    # 2) no version-mismatched package may exist
    for p in nupkgs:
        m = NUPKG_RE.match(p.name)
        if not m:
            errors.append(f"unexpected file name format: {p.name}")
            continue
        if m.group("ver") != ver:
            errors.append(f"version mismatch: {p.name} (expected {ver})")

    # 3) exact nupkg count
    if len(nupkgs) != args.expect_count:
        errors.append(f"nupkg count should be {args.expect_count}, actual {len(nupkgs)}")

    # 4) symbol packages must pair one-to-one with nupkgs
    snames = {p.name for p in snupkgs}
    for p in nupkgs:
        want = p.name.replace(".nupkg", ".snupkg")
        if want not in snames:
            errors.append(f"missing symbol package: {want}")
    if len(snupkgs) != len(nupkgs):
        errors.append(
            f"symbol package count ({len(snupkgs)}) does not match nupkg count ({len(nupkgs)})"
        )

    # 5) belt and braces: nuspec id/version inside each package must match its file name
    for p in nupkgs:
        got = read_nuspec(p)
        if got is None:
            errors.append(f"cannot read nuspec: {p.name}")
            continue
        pid, pver = got
        m = NUPKG_RE.match(p.name)
        fid = m.group("base") if m else ""
        if pver != ver:
            errors.append(f"{p.name}: nuspec version is '{pver}', expected {ver}")
        if fid and pid and pid != fid:
            errors.append(f"{p.name}: nuspec id is '{pid}', file name says '{fid}'")

    print("== Release gate ==")
    print(f"  directory    : {root}")
    print(f"  expect ver   : {ver}")
    print(f"  nupkg count  : {len(nupkgs)}")
    print(f"  snupkg count : {len(snupkgs)}")
    for p in nupkgs:
        print(f"    - {p.name}")

    if errors:
        print("")
        for e in errors:
            print(f"[FAIL] {e}")
        print("")
        print(f"Gate FAILED with {len(errors)} problem(s) - aborted, nothing was pushed.")
        return 1

    print("")
    print(f"[OK] Gate passed: {len(nupkgs)} package(s) + {len(snupkgs)} symbol package(s), "
          f"all at version {ver}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
