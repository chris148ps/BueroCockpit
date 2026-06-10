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

echo
echo "Release vorbereitet für Version $VERSION"
echo
echo "Nächste manuelle Schritte:"
echo "git status"
echo "git add BueroCockpit.csproj"
echo "git commit -m \"Release $VERSION vorbereiten\""
echo "git tag v$VERSION"
echo "git push origin main"
echo "git push origin v$VERSION"
echo "gh release create v$VERSION --title \"BueroCockpit $VERSION\" --notes \"Neue Version von BueroCockpit.\" publish/BueroCockpit-windows-x64.zip publish/BueroCockpit-windows-arm64.zip"
