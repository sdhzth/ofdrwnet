#!/usr/bin/env bash
# ============================================================================
# NetOfd —— NuGet 发布脚本
#
# 用途：把 artifacts/nupkg 下的包按「被依赖者在前」的顺序推送到 nuget.org。
#
# 前置条件：
#   1. 在 https://www.nuget.org/account/apikeys 生成 API Key（勾选
#      "Push packages" 与 "Unlist packages"）
#   2. 设置环境变量 NUGET_API_KEY（或在本脚本提示时粘贴）
#   3. 已完成 dotnet pack netofd.slnx -c Release -o artifacts/nupkg
#
# 用法：
#   ./tools/publish-nuget.sh                    # 推送到 nuget.org
#   ./tools/publish-nuget.sh --source <url>     # 推送到私有源（如 Azure Artifacts）
#   ./tools/publish-nuget.sh --skip-symbols     # 不推 snupkg
#   ./tools/publish-nuget.sh --dry-run          # 只做前置检查，不实际推送
#
# 依赖顺序（由各包 nuspec 的 dependency 拓扑排序得出）：
#   NetOfd.Core → Font → Pkg → Reader → Sign → Crypto → Archive →
#   Layout → Graphics2D → Converter → Emr → Tool → NetOfd → NetOfd.Full
# ============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PKG_DIR="$REPO_ROOT/artifacts/nupkg"
SOURCE="https://api.nuget.org/v3/index.json"
PUSH_SYMBOLS=1
DRY_RUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)       SOURCE="$2"; shift 2 ;;
    --skip-symbols) PUSH_SYMBOLS=0; shift ;;
    --dry-run)      DRY_RUN=1; shift ;;
    -h|--help)      sed -n '2,30p' "$0"; exit 0 ;;
    *) echo "未知参数: $1"; exit 1 ;;
  esac
done

echo "════════════════════════════════════════════════════════════"
echo " NetOfd NuGet 发布"
echo " 源     : $SOURCE"
echo " 包目录 : $PKG_DIR"
echo "════════════════════════════════════════════════════════════"

# ── 前置检查 1：包目录存在 ────────────────────────────────────────────────
if [[ ! -d "$PKG_DIR" ]]; then
  echo "✘ 找不到包目录: $PKG_DIR"
  echo "  请先执行: dotnet pack netofd.slnx -c Release -o artifacts/nupkg"
  exit 1
fi

# ── 前置检查 2：API Key ──────────────────────────────────────────────────
if [[ -z "${NUGET_API_KEY:-}" ]]; then
  if [[ "$DRY_RUN" == "1" ]]; then
    echo "⚠ dry-run：未设置 NUGET_API_KEY（实际推送时需要）"
  else
    echo "未设置 NUGET_API_KEY。"
    echo "  获取方式: https://www.nuget.org/account/apikeys"
    printf "请粘贴 API Key（输入不回显，留空则取消）: "
    read -rs NUGET_API_KEY
    echo
    if [[ -z "$NUGET_API_KEY" ]]; then
      echo "✘ 未提供 API Key，已取消。"
      exit 1
    fi
  fi
fi

# ── 前置检查 3：版本号一致性 ────────────────────────────────────────────
VERSIONS=$(ls "$PKG_DIR"/*.nupkg 2>/dev/null | grep -v '\.snupkg$' \
           | sed -E 's/.*\.([0-9]+\.[0-9]+\.[0-9]+[^.]*)\.nupkg/\1/' | sort -u)
VER_COUNT=$(echo "$VERSIONS" | grep -c . )
if [[ "$VER_COUNT" != "1" ]]; then
  echo "✘ 检测到 $VER_COUNT 个不同版本号，请确保同批发布版本一致:"
  echo "$VERSIONS" | sed 's/^/    /'
  exit 1
fi
VERSION="$VERSIONS"
echo "✓ 版本号一致: $VERSION"

# ── 依赖顺序（硬编码拓扑序，新增包时需同步维护）────────────────────────
ORDER=(
  NetOfd.Core
  NetOfd.Font
  NetOfd.Pkg
  NetOfd.Reader
  NetOfd.Sign
  NetOfd.Crypto
  NetOfd.Archive
  NetOfd.Layout
  NetOfd.Graphics2D
  NetOfd.Converter
  NetOfd.Emr
  NetOfd.Tool
  NetOfd
  NetOfd.Full
)

# ── 前置检查 4：所有包齐备 ──────────────────────────────────────────────
MISSING=0
for id in "${ORDER[@]}"; do
  f="$PKG_DIR/$id.$VERSION.nupkg"
  if [[ ! -f "$f" ]]; then
    echo "✘ 缺少包: $(basename "$f")"
    MISSING=1
  fi
done
if [[ "$MISSING" == "1" ]]; then
  echo "  依赖顺序不完整（缺依赖者时先推依赖包会被 nuget.org 拒绝）。"
  exit 1
fi
echo "✓ 14 个包齐备"

# ── 前置检查 5：README 已打包（NuGet 包详情页会展示）────────────────────
for id in "${ORDER[@]}"; do
  if ! unzip -l "$PKG_DIR/$id.$VERSION.nupkg" 2>/dev/null | grep -q " README.md"; then
    echo "⚠ $id.$VERSION.nupkg 未包含 README.md（NuGet 详情页将无说明）"
  fi
done

# ── 推送 ────────────────────────────────────────────────────────────────
if [[ "$DRY_RUN" == "1" ]]; then
  echo ""
  echo "── dry-run：将按以下顺序推送 ──"
  i=0
  for id in "${ORDER[@]}"; do
    i=$((i+1))
    printf "  %2d. %s.%s.nupkg\n" "$i" "$id" "$VERSION"
  done
  if [[ "$PUSH_SYMBOLS" == "1" ]]; then
    echo "  + 14 个 .snupkg 符号包"
  fi
  echo ""
  echo "dry-run 结束，未实际推送。"
  exit 0
fi

echo ""
OK=0; FAIL=0; SKIP=0
for id in "${ORDER[@]}"; do
  f="$PKG_DIR/$id.$VERSION.nupkg"
  printf "→ 推送 %-40s " "$id.$VERSION.nupkg"
  out=$(dotnet nuget push "$f" --source "$SOURCE" --api-key "$NUGET_API_KEY" 2>&1)
  rc=$?
  if [[ $rc == 0 ]]; then
    echo "✔ 成功"; OK=$((OK+1))
  else
    # 版本已存在视为跳过（非致命）
    if echo "$out" | grep -qi "already exists\|409"; then
      echo "⊘ 已存在，跳过"; SKIP=$((SKIP+1))
    else
      echo "✘ 失败"; echo "$out" | sed 's/^/      /' | head -6; FAIL=$((FAIL+1))
    fi
  fi
  sleep 2   # 避免触发 nuget.org 频率限制
done

# ── 符号包 ──────────────────────────────────────────────────────────────
if [[ "$PUSH_SYMBOLS" == "1" ]]; then
  echo ""
  echo "── 推送符号包（snupkg）──"
  for id in "${ORDER[@]}"; do
    f="$PKG_DIR/$id.$VERSION.snupkg"
    [[ -f "$f" ]] || continue
    printf "→ 推送 %-40s " "$id.$VERSION.snupkg"
    out=$(dotnet nuget push "$f" --source "$SOURCE" --api-key "$NUGET_API_KEY" --no-symbols 2>&1)
    if [[ $? == 0 ]]; then
      echo "✔ 成功"; OK=$((OK+1))
    else
      if echo "$out" | grep -qi "already exists\|409"; then
        echo "⊘ 已存在，跳过"; SKIP=$((SKIP+1))
      else
        echo "✘ 失败"; echo "$out" | sed 's/^/      /' | head -4; FAIL=$((FAIL+1))
      fi
    fi
    sleep 2
  done
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo " 成功 $OK / 跳过 $SKIP / 失败 $FAIL"
echo "════════════════════════════════════════════════════════════"
[[ $FAIL == 0 ]] || exit 1
