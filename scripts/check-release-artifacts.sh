#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_ROOT="$PROJECT_ROOT/publish"
PROJECT_FILE="$PROJECT_ROOT/BueroCockpit.csproj"
APP_ID="BueroCockpitApp"

VERSION="$(perl -ne 'if (/<Version>([^<]+)<\/Version>/) { print $1; exit }' "$PROJECT_FILE")"
if [ -z "$VERSION" ]; then
  echo "Projektversion konnte nicht aus $PROJECT_FILE gelesen werden."
  exit 1
fi

missing=0

check_file() {
  local label="$1"
  local path="$2"

  if [ -f "$path" ]; then
    echo "OK: $label"
    echo "    $path"
  else
    echo "FEHLT: $label"
    echo "       $path"
    missing=1
  fi
}

check_optional_file() {
  local label="$1"
  local path="$2"

  if [ -f "$path" ]; then
    echo "OK (optional): $label"
    echo "    $path"
  else
    echo "NICHT VORHANDEN (optional): $label"
    echo "    $path"
  fi
}

check_glob() {
  local label="$1"
  local pattern="$2"
  local matches=()

  while IFS= read -r match; do
    matches+=("$match")
  done < <(compgen -G "$pattern" || true)

  if [ "${#matches[@]}" -gt 0 ]; then
    echo "OK: $label"
    printf '    %s\n' "${matches[@]}"
  else
    echo "FEHLT: $label"
    echo "       $pattern"
    missing=1
  fi
}

check_velopack_runtime() {
  local runtime="$1"
  local dir="$PUBLISH_ROOT/velopack/$runtime"

  echo
  echo "Velopack $runtime:"

  if [ ! -d "$dir" ]; then
    echo "FEHLT: Ausgabeordner"
    echo "       $dir"
    missing=1
    return
  fi

  check_file "RELEASES-Datei" "$dir/RELEASES-$runtime"
  check_file "Release-JSON" "$dir/releases.$runtime.json"
  check_file "Asset-JSON" "$dir/assets.$runtime.json"
  check_file "Full-NuGet-Paket" "$dir/$APP_ID-$VERSION-$runtime-full.nupkg"
  check_file "Setup-EXE" "$dir/$APP_ID-$runtime-Setup.exe"
}

echo "Prüfe Release-Artefakte..."
echo

check_optional_file "Inno-Setup-Installer" "$PUBLISH_ROOT/installer/BueroCockpitSetup.exe"

check_velopack_runtime "win-x64"

echo
if [ "$missing" -ne 0 ]; then
  echo "Release-Artefakte sind unvollständig."
  echo "Hinweis: Der Inno-Installer wird auf Windows mit scripts/build-installer-windows.ps1 erzeugt."
  exit 1
fi

echo "Alle erwarteten Release-Artefakte wurden gefunden."
