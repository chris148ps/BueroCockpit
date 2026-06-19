#!/usr/bin/env bash
set -euo pipefail

UPDATE_REPO="chris148ps/BueroCockpit-Updates"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

if [ "$#" -ne 1 ]; then
  echo "Verwendung: ./scripts/release-update.sh 0.2.6"
  exit 1
fi

VERSION="$1"
TAG="v$VERSION"

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Ungültige Version: $VERSION"
  echo "Erwartet wird SemVer, z. B. 0.2.6"
  exit 1
fi

echo "=== Vorprüfung ==="

if ! command -v gh >/dev/null 2>&1; then
  echo "FEHLT: GitHub CLI gh"
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "FEHLT: dotnet"
  exit 1
fi

if git rev-parse "$TAG" >/dev/null 2>&1; then
  echo "FEHLER: Git-Tag existiert bereits: $TAG"
  exit 1
fi

if gh release view "$TAG" --repo "$UPDATE_REPO" >/dev/null 2>&1; then
  echo "FEHLER: Release existiert bereits im Update-Repo: $TAG"
  exit 1
fi

git fetch origin main --tags

if [ -n "$(git status --porcelain)" ]; then
  echo "FEHLER: Git-Arbeitsbaum ist nicht sauber."
  git status --short
  exit 1
fi

echo "=== Release-Artefakte bauen ==="
rm -rf publish
./scripts/release.sh "$VERSION"

echo
echo "=== Artefakte prüfen ==="
./scripts/check-release-artifacts.sh || true

echo
echo "=== Versionsänderung committen ==="
git add BueroCockpit.csproj installer/BueroCockpit.iss

if [ -n "$(git status --porcelain)" ]; then
  git commit -m "Version $VERSION vorbereiten"
else
  echo "Keine Versionsänderung zum Committen gefunden."
fi

echo
echo "=== Tag und Push ==="
git tag "$TAG"
git push origin main
git push origin "$TAG"

echo
echo "=== Öffentlichen Update-Release erstellen ==="
gh release create "$TAG" \
  --repo "$UPDATE_REPO" \
  --title "BüroCockpit $VERSION" \
  --notes "Öffentliche Updatepakete für BüroCockpit $VERSION.

Hinweis:
Dieses Release enthält nur fertige Updatepakete. Der Quellcode liegt weiterhin im privaten Repository." \
  publish/velopack/win-x64/BueroCockpit-win-x64-Setup.exe \
  publish/velopack/win-x64/BueroCockpit-win-x64-Portable.zip \
  publish/velopack/win-x64/BueroCockpit-"$VERSION"-win-x64-full.nupkg \
  publish/velopack/win-x64/RELEASES-win-x64 \
  publish/velopack/win-x64/releases.win-x64.json \
  publish/velopack/win-x64/assets.win-x64.json

echo
echo "=== Öffentlichen Release prüfen ==="
gh release view "$TAG" --repo "$UPDATE_REPO"

echo
echo "=== Erreichbarkeit x64 Setup prüfen ==="
curl -I -L "https://github.com/$UPDATE_REPO/releases/download/$TAG/BueroCockpit-win-x64-Setup.exe" | head

echo
echo "=== Git Status ==="
git status --short

echo
echo "Release $VERSION wurde veröffentlicht."
echo "Download:"
echo "https://github.com/$UPDATE_REPO/releases/download/$TAG/BueroCockpit-win-x64-Setup.exe"
