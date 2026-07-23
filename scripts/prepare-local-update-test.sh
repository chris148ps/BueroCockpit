#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$PROJECT_ROOT/BueroCockpit.csproj"
TEST_ROOT="$PROJECT_ROOT/publish/local-update-test"
BASE_PUBLISH="$TEST_ROOT/base-publish"
INITIAL_DIR="$TEST_ROOT/initial"
FEED_DIR="$TEST_ROOT/feed"
CURRENT_VELOPACK="$PROJECT_ROOT/publish/velopack/win-x64"
APP_ID="BueroCockpitApp"
APP_TITLE="BüroCockpit"
APP_AUTHOR="Christian Stange"
MAIN_EXE="BueroCockpit.exe"
APP_ICON="$PROJECT_ROOT/Assets/BueroCockpit.ico"
BASE_VERSION="${1:-0.4.22}"

cd "$PROJECT_ROOT"

if ! command -v vpk >/dev/null 2>&1; then
  echo "Velopack CLI 'vpk' wurde nicht gefunden."
  echo "Bitte den Ordner des globalen dotnet-Tools zu PATH hinzufügen."
  exit 1
fi

TARGET_VERSION="$(perl -ne 'if (/<Version>([^<]+)<\/Version>/) { print $1; exit }' "$PROJECT_FILE")"
if [ -z "$TARGET_VERSION" ]; then
  echo "Projektversion konnte nicht aus $PROJECT_FILE gelesen werden."
  exit 1
fi

if [ ! -s "$CURRENT_VELOPACK/$APP_ID-$TARGET_VERSION-win-x64-full.nupkg" ] ||
   [ ! -s "$CURRENT_VELOPACK/releases.win-x64.json" ]; then
  echo "Die Zielartefakte für $TARGET_VERSION fehlen."
  echo "Bitte zuerst ausführen:"
  echo "  ./scripts/publish-windows.sh"
  echo "  ./scripts/package-velopack-windows.sh"
  exit 1
fi

rm -rf "$TEST_ROOT"
mkdir -p "$BASE_PUBLISH" "$INITIAL_DIR" "$FEED_DIR"

echo "Erzeuge isolierte Velopack-Testbasis $BASE_VERSION..."
dotnet publish \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -p:Version="$BASE_VERSION" \
  -p:AssemblyVersion="$BASE_VERSION.0" \
  -p:FileVersion="$BASE_VERSION.0" \
  -o "$BASE_PUBLISH"

find "$BASE_PUBLISH" -type f -name "*.pdb" -delete

vpk '[win]' pack \
  --packId "$APP_ID" \
  --packTitle "$APP_TITLE" \
  --packAuthors "$APP_AUTHOR" \
  --packVersion "$BASE_VERSION" \
  --packDir "$BASE_PUBLISH" \
  --mainExe "$MAIN_EXE" \
  --icon "$APP_ICON" \
  --runtime win-x64 \
  --channel win-x64 \
  --outputDir "$INITIAL_DIR"

cp "$CURRENT_VELOPACK/$APP_ID-$TARGET_VERSION-win-x64-full.nupkg" "$FEED_DIR/"
cp "$CURRENT_VELOPACK/RELEASES-win-x64" "$FEED_DIR/"
cp "$CURRENT_VELOPACK/releases.win-x64.json" "$FEED_DIR/"
cp "$CURRENT_VELOPACK/assets.win-x64.json" "$FEED_DIR/"

cat > "$TEST_ROOT/README.txt" <<EOF
BüroCockpit lokaler Velopack-Update-Test

Basisversion: $BASE_VERSION
Zielversion:  $TARGET_VERSION

Initialer Installer:
$INITIAL_DIR/$APP_ID-win-x64-Setup.exe

Lokaler Update-Feed:
$FEED_DIR

Der Test muss auf Windows mit isolierten Pfaden gestartet werden:

  BUEROCOCKPIT_DATA_DIRECTORY=<isolierter Testordner>
  BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY=<isolierter lokaler Konfigurationsordner>

Kein Inhalt aus %LOCALAPPDATA%\\BueroCockpit darf für diesen Update-Test
verändert werden.
EOF

echo
echo "Lokaler Velopack-Update-Test vorbereitet:"
echo "  $TEST_ROOT"
echo "  Basis-Installer: $INITIAL_DIR/$APP_ID-win-x64-Setup.exe"
echo "  Ziel-Feed: $FEED_DIR"
