#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$PROJECT_ROOT/publish/windows-x64"

cd "$PROJECT_ROOT"

echo "Erstelle Windows-x64-Release..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Avalonia und PDFtoImage nutzen native Abhängigkeiten. SingleFile bleibt praktikabel,
# wenn native Bibliotheken beim Start in einen temporären Ordner extrahiert werden.
dotnet publish \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$OUTPUT_DIR"

echo
echo "Windows-Publish erstellt:"
echo "$OUTPUT_DIR"
