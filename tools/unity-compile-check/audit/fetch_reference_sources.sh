#!/usr/bin/env bash
# Fetch (read-only, sparse, shallow) the Unity C# reference source and the Graphics repository that
# match game/ProjectSettings/ProjectVersion.txt, for the API audit. Nothing from these repositories is
# committed or redistributed: the indexer only lists declared API names and their [Obsolete] state
# (reference use, as the Unity Reference-Only License allows). The cache directory is git-ignored.
set -euo pipefail
CACHE="${1:?usage: fetch_reference_sources.sh <cache-dir>}"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$REPO/game/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
MAJOR_MINOR="$(echo "$VERSION" | cut -d. -f1,2)"   # 6000.3
mkdir -p "$CACHE"

fetch() { # <dir> <url> <ref> <sparse paths...>
  local dir="$1" url="$2" ref="$3"; shift 3
  if [[ -f "$dir/.ghumante-ref" && "$(cat "$dir/.ghumante-ref")" == "$ref" ]]; then
    echo "reference source cached: $dir ($ref)"; return
  fi
  rm -rf "$dir"
  git clone --quiet --depth 1 --branch "$ref" --filter=blob:none --no-checkout "$url" "$dir"
  git -C "$dir" sparse-checkout init --no-cone
  git -C "$dir" sparse-checkout set "$@"
  git -C "$dir" checkout --quiet
  echo "$ref" > "$dir/.ghumante-ref"
  echo "fetched $url @ $ref"
}

fetch "$CACHE/UnityCsReference" https://github.com/Unity-Technologies/UnityCsReference.git "$VERSION" \
  '/Editor/*' '/Runtime/*' '/Modules/*'
fetch "$CACHE/Graphics" https://github.com/Unity-Technologies/Graphics.git "$MAJOR_MINOR/staging" \
  '/Packages/com.unity.render-pipelines.universal/Runtime/*' '/Packages/com.unity.render-pipelines.core/Runtime/*'
