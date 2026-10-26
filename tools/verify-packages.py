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
# ⚠️ 版本段必须严格按 SemVer 三段式匹配：包 ID 本身含点号（NetOfd.Core），
#    若版本段允许任意字符会被贪婪拆成 id="NetOfd.Core.1.3" / ver="1"（实测踩过）。
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
    ap = argparse.ArgumentParser(description="校验发包产物：数量、版本一致性、符号包配对")
    ap.add_argument("dir", help="打包产物目录，如 artifacts/nupkgs")
    ap.add_argument("version", help="期望版本号，如 1.3.1")
    ap.add_argument("--expect-count", type=int, default=len(EXPECTED_PACKAGES),
                    help="期望的 nupkg 数量（默认 %(default)s）")
    args = ap.parse_args()

    root = Path(args.dir)
    ver = args.version
    errors: list[str] = []
    notes: list[str] = []

    if not root.is_dir():
        print(f"✘ 产物目录不存在: {root}")
        return 1

    nupkgs = sorted(p for p in root.glob("*.nupkg"))
    snupkgs = sorted(p for p in root.glob("*.snupkg"))

    if not nupkgs:
        print(f"✘ {root} 下没有任何 .nupkg")
        return 1

    # ① 期望的包必须齐备
    names = {p.name for p in nupkgs}
    for pid in EXPECTED_PACKAGES:
        want = f"{pid}.{ver}.nupkg"
        if want not in names:
            errors.append(f"缺少包: {want}")

    # ② 不得存在版本错位的包（如硬编码旧 Version 产出的包）
    for p in nupkgs:
        m = NUPKG_RE.match(p.name)
        if not m:
            errors.append(f"文件名格式异常: {p.name}")
            continue
        if m.group("ver") != ver:
            errors.append(f"版本错位的包: {p.name}（期望版本 {ver}）")

    # ③ nupkg 数量必须正好
    if len(nupkgs) != args.expect_count:
        errors.append(f"nupkg 数量应为 {args.expect_count}，实际 {len(nupkgs)}")

    # ④ 符号包与 nupkg 一一配对
    snames = {p.name for p in snupkgs}
    for p in nupkgs:
        want = p.name.replace(".nupkg", ".snupkg")
        if want not in snames:
            errors.append(f"缺少符号包: {want}")
    if len(snupkgs) != len(nupkgs):
        errors.append(f"符号包数量({len(snupkgs)})与 nupkg 数量({len(nupkgs)})不匹配")

    # ⑤ 双保险：包内 nuspec 的 id/version 必须与文件名一致
    for p in nupkgs:
        got = read_nuspec(p)
        if got is None:
            errors.append(f"无法读取 nuspec: {p.name}")
            continue
        pid, pver = got
        m = NUPKG_RE.match(p.name)
        fid = m.group("base") if m else ""
        if pver != ver:
            errors.append(f"{p.name} 内含 nuspec 版本 '{pver}'，与期望 {ver} 不一致")
        if fid and pid and pid != fid:
            errors.append(f"{p.name} 内含 nuspec id '{pid}'，与文件名 '{fid}' 不一致")

    print(f"── 发包门禁：{root} ──")
    print(f"  期望版本   : {ver}")
    print(f"  nupkg      : {len(nupkgs)} 个")
    print(f"  snupkg     : {len(snupkgs)} 个")
    for p in nupkgs:
        print(f"    · {p.name}")
    if notes:
        for n in notes:
            print(f"  提示: {n}")
    if errors:
        print("")
        for e in errors:
            print(f"✘ {e}")
        print("")
        print(f"门禁未通过（{len(errors)} 项问题）—— 已中止，不会推送任何包。")
        return 1

    print("")
    print(f"✔ 门禁通过：{len(nupkgs)} 个包 + {len(snupkgs)} 个符号包，版本统一为 {ver}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
