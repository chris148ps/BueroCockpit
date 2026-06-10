#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

./scripts/publish-windows.sh

for dir in "$PROJECT_ROOT/publish/windows-x64" "$PROJECT_ROOT/publish/windows-arm64"; do
  if [ ! -d "$dir" ]; then
    echo "Publish-Ordner fehlt: $dir"
    exit 1
  fi
done

echo
echo "Installer-Eingabedateien sind vorbereitet:"
echo "$PROJECT_ROOT/publish/windows-x64"
echo "$PROJECT_ROOT/publish/windows-arm64"
echo
echo "Diese Ordner auf Windows kopieren oder Repository auf Windows auschecken"
echo "und dort ausführen:"
echo "powershell -ExecutionPolicy Bypass -File scripts/build-installer-windows.ps1"
