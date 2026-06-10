#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_ROOT="$PROJECT_ROOT/publish"

cd "$PROJECT_ROOT"

publish_runtime() {
  local runtime="$1"
  local output_dir="$PUBLISH_ROOT/windows-${runtime#win-}"

  echo "Erstelle Windows-${runtime#win-}-Release..."
  rm -rf "$output_dir"
  mkdir -p "$output_dir"

  # Avalonia und PDFtoImage nutzen native Abhängigkeiten. SingleFile bleibt praktikabel,
  # wenn native Bibliotheken beim Start in einen temporären Ordner extrahiert werden.
  dotnet publish \
    -c Release \
    -r "$runtime" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$output_dir"

  echo "Windows-Publish erstellt: $output_dir"
  echo
}

publish_runtime "win-x64"
publish_runtime "win-arm64"

echo "Alle Windows-Publish-Ordner wurden erstellt:"
echo "$PUBLISH_ROOT/windows-x64"
echo "$PUBLISH_ROOT/windows-arm64"
