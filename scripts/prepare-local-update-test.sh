#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_ROOT="$PROJECT_ROOT/publish/local-update-test"

cd "$PROJECT_ROOT"

rm -rf "$TEST_ROOT"
mkdir -p "$TEST_ROOT/initial" "$TEST_ROOT/update" "$TEST_ROOT/feed"

cat <<EOF
Lokaler Velopack-Update-Test wurde vorbereitet:

$TEST_ROOT
$TEST_ROOT/initial
$TEST_ROOT/update
$TEST_ROOT/feed

Nächste manuelle Schritte für einen Test von 0.1.0 auf 0.2.0:

1. Version 0.1.0 veröffentlichen:
   ./scripts/publish-windows.sh
   ./scripts/package-velopack-windows.sh
   cp -R publish/velopack/win-x64/* "$TEST_ROOT/initial/"

2. Testweise die Projektversion in BueroCockpit.csproj auf 0.2.0 setzen.
   Nicht committen, nicht taggen, keinen GitHub Release erstellen.

3. Update-Artefakte erzeugen:
   ./scripts/publish-windows.sh
   ./scripts/package-velopack-windows.sh
   cp -R publish/velopack/win-x64/* "$TEST_ROOT/feed/"

4. Initiale 0.1.0-Version aus "$TEST_ROOT/initial" installieren/starten.

5. In BüroCockpit unter Einstellungen den lokalen Update-Kanal setzen:
   $TEST_ROOT/feed

6. "Nach Updates suchen" ausführen und prüfen, dass Version 0.2.0 gefunden wird.

7. Nach dem Test BueroCockpit.csproj wieder auf die echte Projektversion zurücksetzen.

Dieser Ablauf erstellt keinen GitHub Release, keine Tags und führt keinen Push aus.
AppData, Datenbank, Anhänge und Backups bleiben außerhalb der Programmdateien.
EOF
