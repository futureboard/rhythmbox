#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNTIME="${1:-linux-x64}"
CONFIG="${CONFIGURATION:-Release}"
VERSION="$(grep -oP '(?<=<Version>)[^<]+' "$ROOT/Directory.Build.props" | head -n1)"
PUBLISH_DIR="${OUTPUT_ROOT:-$ROOT/out/publish}/$RUNTIME"

rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

PROJECTS=(
  "src/Rythmbox.App/Rythmbox.App.csproj"
  "src/Rythmbox.Editor/Rythmbox.Editor.csproj"
  "src/Rythmbox.SampleCreator/Rythmbox.SampleCreator.csproj"
)

echo "Publishing Rythmbox ${VERSION} (${RUNTIME}) to ${PUBLISH_DIR}"

for project in "${PROJECTS[@]}"; do
  echo "-> ${project}"
  dotnet publish "$ROOT/$project" \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"
done

echo "Publish complete: ${PUBLISH_DIR}"
