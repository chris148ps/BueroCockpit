#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_ROOT="$PROJECT_ROOT/publish"

package_runtime() {
  local arch="$1"
  local publish_dir="$PUBLISH_ROOT/windows-$arch"
  local zip_path="$PUBLISH_ROOT/BueroCockpit-windows-$arch.zip"

  if [ ! -d "$publish_dir" ]; then
    echo "Publish-Ordner nicht gefunden: $publish_dir"
    echo "Bitte zuerst ausführen: ./scripts/publish-windows.sh"
    exit 1
  fi

  mkdir -p "$PUBLISH_ROOT"
  rm -f "$zip_path"

  echo "Erstelle ZIP für Windows-$arch..."
  cd "$publish_dir"
  zip -r "$zip_path" . \
    -x "*.db" "*.db-shm" "*.db-wal" \
    -x "Backups/*" "Tasks/*" "AppData/*" "Daten/*" "Testdaten/*"

  echo "Windows-ZIP erstellt: $zip_path"
  echo
}

package_runtime "x64"
package_runtime "arm64"

echo "Alle Windows-ZIP-Pakete wurden erstellt:"
echo "$PUBLISH_ROOT/BueroCockpit-windows-x64.zip"
echo "$PUBLISH_ROOT/BueroCockpit-windows-arm64.zip"
