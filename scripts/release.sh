#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$PROJECT_ROOT/BueroCockpit.csproj"

cd "$PROJECT_ROOT"

if [ "$#" -ne 1 ]; then
  echo "Verwendung: ./scripts/release.sh 0.2.0"
  exit 1
fi

VERSION="$1"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Ungültige Version: $VERSION"
  echo "Erwartet wird grobes SemVer, z. B. 0.2.0"
  exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
  echo "Git-Arbeitsbaum ist nicht sauber. Bitte erst committen oder verwerfen."
  git status --short
  exit 1
fi

ASSEMBLY_VERSION="$VERSION.0"

update_or_insert_property() {
  local name="$1"
  local value="$2"

  if grep -q "<$name>" "$PROJECT_FILE"; then
    perl -0pi -e "s#<$name>.*?</$name>#<$name>$value</$name>#s" "$PROJECT_FILE"
  else
    perl -0pi -e "s#(<ImplicitUsings>.*?</ImplicitUsings>)#\$1\n    <$name>$value</$name>#s" "$PROJECT_FILE"
  fi
}

update_or_insert_property "Version" "$VERSION"
update_or_insert_property "AssemblyVersion" "$ASSEMBLY_VERSION"
update_or_insert_property "FileVersion" "$ASSEMBLY_VERSION"

dotnet build
./scripts/publish-windows.sh
./scripts/package-windows.sh
./scripts/package-velopack-windows.sh

echo
echo "Release vorbereitet für Version $VERSION"
echo
echo "Erzeugte/relevante Artefakte:"
echo "- ZIP-Pakete:"
echo "  publish/BueroCockpit-windows-x64.zip"
echo "- Inno-Installer, falls auf Windows gebaut:"
echo "  publish/installer/BueroCockpitSetup.exe"
echo "- Velopack x64:"
echo "  publish/velopack/win-x64/"
echo
echo "Artefakte prüfen:"
echo "./scripts/check-release-artifacts.sh"
echo
echo "Nächste manuelle Schritte:"
echo "git status"
echo "git add BueroCockpit.csproj"
echo "git commit -m \"Release $VERSION vorbereiten\""
echo "git tag v$VERSION"
echo "git push origin main"
echo "git push origin v$VERSION"
echo
echo "GitHub Release manuell erstellen und Artefakte anhängen."
echo "Nicht automatisch ausgeführt:"
echo "gh release create v$VERSION --title \"BüroCockpit $VERSION\" --notes \"Neue Version von BüroCockpit.\" \\"
echo "  publish/BueroCockpit-windows-x64.zip \\"
echo "  publish/installer/BueroCockpitSetup.exe \\"
echo "  publish/velopack/win-x64/*"
echo
echo "Hinweis: publish/installer/BueroCockpitSetup.exe entsteht auf Windows mit Inno Setup."
