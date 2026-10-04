#!/usr/bin/env bash
# Compile the Unity C# under game/Assets/Ghumante without Unity (see README.md).
#
#   tools/unity-compile-check/run.sh            # generate projects, build for Android and iOS defines
#   tools/unity-compile-check/run.sh --audit    # also verify every Unity API we use against Unity's
#                                               # 6000.3 C# reference source (clones it, ~100 MB, cached)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET="${DOTNET:-$(command -v dotnet || echo /root/.dotnet/dotnet)}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

AUDIT=0
for arg in "$@"; do
  case "$arg" in
    --audit) AUDIT=1 ;;
    *) echo "unknown argument: $arg" >&2; exit 2 ;;
  esac
done

python3 "$HERE/generate.py"
cd "$HERE/generated"
"$DOTNET" new sln -n CompileCheck --force >/dev/null
"$DOTNET" sln CompileCheck.sln add ./*/*.csproj >/dev/null
expected=$(find . -mindepth 2 -maxdepth 2 -name "*.csproj" | wc -l)
actual=$("$DOTNET" sln CompileCheck.sln list | grep -c '\.csproj$' || true)
if [[ "$expected" -ne "$actual" ]]; then
  echo "compile check: only $actual of $expected generated projects could be loaded" >&2
  exit 1
fi

EXTRA=()
if [[ $AUDIT -eq 1 ]]; then
  INDEX="$HERE/.cache/unity-api-index.tsv"
  "$HERE/audit/fetch_reference_sources.sh" "$HERE/.cache"
  "$DOTNET" run --project "$HERE/audit/Indexer/Indexer.csproj" -c Release -- \
      "$INDEX" "$HERE/.cache/UnityCsReference" "$HERE/.cache/Graphics/Packages"
  "$DOTNET" build "$HERE/audit/Analyzer/Analyzer.csproj" -c Release -nologo -v q
  EXTRA=(-p:UnityApiIndex="$INDEX")
fi

status=0
for platform in UNITY_ANDROID UNITY_IOS; do
  echo "== compile check: $platform"
  # --no-incremental: the two passes differ only in defines, so never reuse the previous pass's output.
  if ! "$DOTNET" build CompileCheck.sln -nologo -v q --no-incremental -p:UnityPlatformDefine=$platform ${EXTRA[@]+"${EXTRA[@]}"}; then
    status=1
  fi
done

python3 "$HERE/check_ui.py" || status=1
exit $status
