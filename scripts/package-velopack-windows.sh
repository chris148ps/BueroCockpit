#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$PROJECT_ROOT/BueroCockpit.csproj"
PUBLISH_ROOT="$PROJECT_ROOT/publish"
VELOPACK_ROOT="$PUBLISH_ROOT/velopack"
APP_ID="BueroCockpit"
APP_TITLE="BüroCockpit"
MAIN_EXE="BueroCockpit.exe"

cd "$PROJECT_ROOT"

if ! command -v vpk >/dev/null 2>&1; then
  echo "Velopack CLI 'vpk' wurde nicht gefunden."
  echo "Installation:"
  echo "dotnet tool install -g vpk"
  exit 1
fi

VERSION="$(perl -ne 'if (/<Version>([^<]+)<\/Version>/) { print $1; exit }' "$PROJECT_FILE")"
if [ -z "$VERSION" ]; then
  echo "Projektversion konnte nicht aus $PROJECT_FILE gelesen werden."
  exit 1
fi

pack_runtime() {
  local runtime="$1"
  local output_name="$2"
  local pack_dir="$PUBLISH_ROOT/windows-${runtime#win-}"
  local output_dir="$VELOPACK_ROOT/$output_name"

  if [ ! -d "$pack_dir" ]; then
    echo "Publish-Ordner fehlt: $pack_dir"
    echo "Bitte zuerst ausführen: ./scripts/publish-windows.sh"
    exit 1
  fi

  if [ ! -f "$pack_dir/$MAIN_EXE" ]; then
    echo "Haupt-EXE fehlt: $pack_dir/$MAIN_EXE"
    exit 1
  fi

  echo "Erstelle Velopack-Paket für $runtime..."
  rm -rf "$output_dir"
  mkdir -p "$output_dir"

  vpk '[win]' pack \
    --packId "$APP_ID" \
    --packTitle "$APP_TITLE" \
    --packVersion "$VERSION" \
    --packDir "$pack_dir" \
    --mainExe "$MAIN_EXE" \
    --runtime "$runtime" \
    --channel "$runtime" \
    --outputDir "$output_dir"

  echo "Velopack-Ausgabe erstellt: $output_dir"
  echo
}

pack_runtime "win-x64" "win-x64"
pack_runtime "win-arm64" "win-arm64"

echo "Velopack-Pakete wurden erstellt:"
echo "$VELOPACK_ROOT/win-x64"
echo "$VELOPACK_ROOT/win-arm64"
