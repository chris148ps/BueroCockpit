#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_DIR="$PROJECT_ROOT/publish/windows-x64"
ZIP_PATH="$PROJECT_ROOT/publish/BueroCockpit-windows-x64.zip"

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "Publish-Ordner nicht gefunden: $PUBLISH_DIR"
  echo "Bitte zuerst ausführen: ./scripts/publish-windows.sh"
  exit 1
fi

mkdir -p "$PROJECT_ROOT/publish"
rm -f "$ZIP_PATH"

cd "$PUBLISH_DIR"
zip -r "$ZIP_PATH" . \
  -x "*.db" "*.db-shm" "*.db-wal" \
  -x "Backups/*" "Tasks/*" "AppData/*" "Daten/*" "Testdaten/*"

echo
echo "Windows-ZIP erstellt:"
echo "$ZIP_PATH"
