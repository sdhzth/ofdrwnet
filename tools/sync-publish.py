#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NetOfd 开发态 → 发布态同步脚本
================================================================================

背景
----
本仓库存在两份拷贝：

  E:\\电子病历\\netofd\\              开发态（无 git），日常改动都在这里
  E:\\电子病历\\netofd\\github-upload\\  发布态（git 仓库），最终推送到 GitHub

手工两边拷贝容易漏文件（尤其是删除操作），本脚本负责自动同步。

策略
----
* **白名单**：只同步下列路径，其余一律不管
      src/  tests/  samples/  tools/  .github/
      netofd.slnx  Directory.Build.props  Directory.Packages.props
      README.md  README.en.md
* **发布态专属文件**：`.gitignore` / `LICENSE` / `LICENSE-Apache-2.0` / `NOTICE`
  只存在于 github-upload，同步时**不覆盖**（但会在报告中列出，便于发现遗漏）
* **排除临时产物**：bin/ obj/ .vs/ TestResults/ artifacts/ render_dump/ __pycache__/
  以及 *.log / smoke_result.txt / build_output.txt 等
* **删除同步**：白名单路径下，发布态里存在但源里没有的文件会被删除
  （例如把 EmrGenerator.cs 从 sample 迁到库后，旧文件要自动清掉）
* **内容比对**：用 sha256 判定新增/修改/删除，避免时间戳噪声导致误报
* **绝不触碰** github-upload/.git

用法
----
    python tools/sync-publish.py                 # 预览（dry-run），只报告不改动
    python tools/sync-publish.py --apply         # 实际同步
    python tools/sync-publish.py --apply --commit "msg"    # 同步后 git 提交
    python tools/sync-publish.py --apply --push             # 同步后提交并推送
    python tools/sync-publish.py --check         # 仅检查是否同步（有差异时退出码 1）

退出码：0 = 正常 / 1 = 有差异（--check 模式）或执行失败
"""

from __future__ import annotations

import argparse
import filecmp
import hashlib
import os
import shutil
import subprocess
import sys
from pathlib import Path

# ── 配置 ────────────────────────────────────────────────────────────────

# 需要同步的路径（相对仓库根）
MANAGED_PATHS = [
    "src",
    "tests",
    "samples",
    "tools",
    ".github",
    "netofd.slnx",
    "Directory.Build.props",
    "Directory.Packages.props",
    "README.md",
    "README.en.md",
]

# 只存在于发布态、同步时不覆盖的文件（会在报告中提示）
PUBLISH_ONLY = [".gitignore", "LICENSE", "LICENSE-Apache-2.0", "NOTICE"]

# 排除的目录名（精确匹配，大小写敏感 —— 避免误伤 samples/RenderDump）
EXCLUDE_DIRS = {
    "bin", "obj", ".vs", "TestResults", "artifacts", "render_dump",
    "__pycache__", ".idea", ".git", "node_modules",
}

# 排除的文件名（精确匹配）
EXCLUDE_FILES = {
    "smoke_result.txt", "build_output.txt", "test_xelement.csx", "xml_test.txt",
    ".DS_Store", "Thumbs.db",
}

# 排除的文件后缀
EXCLUDE_SUFFIX = (".log", ".user", ".suo", ".tmp", ".bak", ".orig")


# ── 工具函数 ────────────────────────────────────────────────────────────

def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def is_excluded(path: Path) -> bool:
    """判断单条路径是否属于临时产物（静态规则，不含 .gitignore）。"""
    if path.name in EXCLUDE_FILES:
        return True
    if path.suffix.lower() in EXCLUDE_SUFFIX:
        return True
    # 任一父目录名被排除（精确匹配）
    for part in path.parts:
        if part in EXCLUDE_DIRS:
            return True
    return False


def gitignored_paths(dst_root: Path, paths: list[str]) -> set[str]:
    """
    用目标仓库的 .gitignore 判定哪些路径会被忽略（gitignore 作为排除规则的唯一来源）。
    一次批量调用 git check-ignore，避免逐文件起进程。

    注意两个坑：
    1. 必须用 -z（NUL 分隔）模式 —— 否则 git 会对含特殊字符的路径加引号转义，
       输出的字符串与输入路径对不上。
    2. 必须以二进制方式传输入 —— Windows 下 text=True 会把 \\n 翻成 \\r\\n，
       git 会把 \\r 当成文件名的一部分。
    """
    if not paths:
        return set()
    try:
        payload = ("\0".join(paths) + "\0").encode("utf-8")
        res = subprocess.run(
            ["git", "-C", str(dst_root), "check-ignore", "--no-index", "--stdin", "-z"],
            input=payload, capture_output=True,
        )
        out = res.stdout.decode("utf-8", errors="replace")
        return {p for p in out.split("\0") if p}
    except Exception:
        return set()


def collect_files(root: Path, rel: str) -> list[Path]:
    """收集源（或目标）某白名单路径下的全部应纳入管理的文件，返回相对路径列表。"""
    target = root / rel
    if not target.exists():
        return []
    if target.is_file():
        return [Path(rel)]

    out: list[Path] = []
    for dirpath, dirnames, filenames in os.walk(target):
        d = Path(dirpath)
        # 就地裁剪目录名，避免下钻 bin/obj
        dirnames[:] = [x for x in dirnames if x not in EXCLUDE_DIRS]
        for fn in filenames:
            p = d / fn
            relp = p.relative_to(root)
            if is_excluded(relp):
                continue
            out.append(relp)
    return out


def resolve_roots() -> tuple[Path, Path]:
    """定位开发态根目录与发布态根目录。"""
    script = Path(__file__).resolve()
    base = script.parent.parent          # tools/ 的上一级 = 仓库根

    # 若脚本位于发布态副本内（.../github-upload/tools/），向上找开发态
    for candidate in [base, base.parent]:
        pub = candidate / "github-upload"
        if pub.is_dir() and (pub / ".git").exists():
            return candidate, pub

    # 兜底：base 自身就是发布态
    if (base / ".git").exists():
        print("提示：脚本运行在发布态副本内（github-upload/），无需同步。", file=sys.stderr)
        print("      开发态脚本路径：<仓库根>/tools/sync-publish.py", file=sys.stderr)
        sys.exit(0)

    print("✘ 找不到 github-upload 目录（需与开发态根目录同级）", file=sys.stderr)
    sys.exit(1)


def git(args: list[str], cwd: Path) -> int:
    cmd = ["git", *args]
    print("  $ " + " ".join(cmd))
    env = dict(os.environ)
    env["GIT_TERMINAL_PROMPT"] = "0"
    return subprocess.call(cmd, cwd=str(cwd), env=env)


# ── 主流程 ──────────────────────────────────────────────────────────────

def main() -> int:
    ap = argparse.ArgumentParser(description="NetOfd 开发态 → 发布态同步")
    ap.add_argument("--apply", action="store_true", help="实际执行同步（默认仅预览）")
    ap.add_argument("--commit", metavar="MSG", help="同步后创建 git 提交")
    ap.add_argument("--push", action="store_true", help="同步后提交并推送")
    ap.add_argument("--check", action="store_true", help="仅检查；存在差异时退出码 1")
    args = ap.parse_args()

    src_root, dst_root = resolve_roots()
    print("=" * 78)
    print(" NetOfd 开发态 → 发布态同步")
    print(f"   源   : {src_root}")
    print(f"   目标 : {dst_root}")
    print(f"   模式 : {'实际执行' if args.apply else '预览（dry-run）'}")
    print("=" * 78)

    added: list[str] = []
    modified: list[str] = []
    deleted: list[str] = []
    skipped: list[str] = []
    unchanged = 0

    for rel in MANAGED_PATHS:
        src_files = collect_files(src_root, rel)
        dst_files = collect_files(dst_root, rel)

        src_set = {p.as_posix() for p in src_files}
        dst_set = {p.as_posix() for p in dst_files}

        # 目标端 .gitignore 判定（唯一排除来源，避免两处规则各写一份）
        ignored = gitignored_paths(dst_root, sorted(src_set | dst_set))
        skipped.extend(sorted(ignored))
        src_set -= ignored
        dst_set -= ignored

        # 新增 / 修改
        for rp in sorted(src_set):
            s = src_root / rp
            d = dst_root / rp
            if rp not in dst_set:
                added.append(rp)
                if args.apply:
                    d.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(s, d)
            elif sha256(s) != sha256(d):
                modified.append(rp)
                if args.apply:
                    shutil.copy2(s, d)

        # 删除（目标有、源没有）
        for rp in sorted(dst_set - src_set):
            deleted.append(rp)
            if args.apply:
                (dst_root / rp).unlink(missing_ok=True)

        unchanged += len(src_set & dst_set) - len([x for x in src_set & dst_set if x in modified])

    # 清理可能残留的空目录
    if args.apply:
        for rel in MANAGED_PATHS:
            base = dst_root / rel
            if base.is_dir():
                for dirpath, dirnames, filenames in os.walk(base, topdown=False):
                    d = Path(dirpath)
                    if d == base:
                        continue
                    if not any(d.iterdir()):
                        try:
                            d.rmdir()
                        except OSError:
                            pass

    # ── 报告 ──────────────────────────────────────────────────────────
    print()
    print(f"新增 {len(added)} 个 / 修改 {len(modified)} 个 / 删除 {len(deleted)} 个 "
          f"/ 未变 {unchanged} 个 / 按 .gitignore 跳过 {len(skipped)} 个")
    if added:
        print("\n── 新增 ──")
        for x in added:
            print(f"  + {x}")
    if modified:
        print("\n── 修改 ──")
        for x in modified:
            print(f"  M {x}")
    if deleted:
        print("\n── 删除 ──")
        for x in deleted:
            print(f"  - {x}")
    if skipped:
        print("\n── 按 .gitignore 跳过（未复制）──")
        for x in skipped[:40]:
            print(f"  ~ {x}")
        if len(skipped) > 40:
            print(f"  …… 另有 {len(skipped) - 40} 个")

    # 发布态专属文件提醒
    missing_publish_only = [p for p in PUBLISH_ONLY if not (dst_root / p).exists()]
    if missing_publish_only:
        print("\n⚠ 发布态缺少专属文件（同步不会自动创建，请手工补齐）:")
        for p in missing_publish_only:
            print(f"    {p}")

    if not (added or modified or deleted):
        print("\n✓ 两边已同步，无需操作。")

    if args.check:
        return 1 if (added or modified or deleted) else 0

    if not args.apply:
        print("\n（预览模式，未做任何改动；加 --apply 执行）")
        return 0

    # ── git 提交 / 推送 ───────────────────────────────────────────────
    if (added or modified or deleted) and (args.commit or args.push):
        print("\n── git 提交 ──")
        git(["add", "-A"], dst_root)
        msg = args.commit or "chore: sync from dev workspace"
        if git(["commit", "-m", msg], dst_root) != 0:
            print("✘ 提交失败", file=sys.stderr)
            return 1
        if args.push:
            print("\n── git 推送 ──")
            if git(["push", "origin", "master"], dst_root) != 0:
                print("✘ 推送失败", file=sys.stderr)
                return 1
            print("✓ 已推送")

    print()
    print("=" * 78)
    return 0


if __name__ == "__main__":
    sys.exit(main())
