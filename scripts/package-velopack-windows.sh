#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_ROOT="$PROJECT_ROOT/publish"
VELOPACK_OUTPUT="$PUBLISH_ROOT/velopack"

cd "$PROJECT_ROOT"

if ! command -v vpk >/dev/null 2>&1; then
  echo "Velopack CLI 'vpk' wurde nicht gefunden."
  echo "Bitte Velopack CLI installieren und danach dieses Skript erneut ausführen."
  echo "Dieses Skript erzwingt noch keine Paketierung, sondern bereitet den Ablauf vor."
  exit 1
fi

for arch in x64 arm64; do
  publish_dir="$PUBLISH_ROOT/windows-$arch"
  if [ ! -d "$publish_dir" ]; then
    echo "Publish-Ordner fehlt: $publish_dir"
    echo "Bitte zuerst ausführen: ./scripts/publish-windows.sh"
    exit 1
  fi
done

mkdir -p "$VELOPACK_OUTPUT"

echo "Velopack CLI gefunden:"
vpk --help
echo
echo "Velopack-Paketierung ist vorbereitet."
echo "Nächster Schritt: finale vpk pack Parameter für App-ID, Version, Runtime und Ausgabeordner festlegen."
echo "Ausgabeordner vorgesehen: $VELOPACK_OUTPUT"
