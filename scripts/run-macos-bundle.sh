#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${1:-Debug}"

case "$(uname -m)" in
  arm64) rid="osx-arm64" ;;
  x86_64) rid="osx-x64" ;;
  *) echo "Nicht unterstützte macOS-Architektur: $(uname -m)" >&2; exit 1 ;;
esac

publish_dir="$repo_root/bin/$configuration/macos-bundle-publish"
bundle_dir="$repo_root/bin/$configuration/BueroCockpit.app"

mkdir -p "$publish_dir" "$bundle_dir/Contents/MacOS"

dotnet publish "$repo_root/BueroCockpit.csproj" \
  -c "$configuration" \
  -r "$rid" \
  --self-contained false \
  -p:UseAppHost=true \
  -p:AppendTargetFrameworkToOutputPath=false \
  -o "$publish_dir"

cp -R "$publish_dir/." "$bundle_dir/Contents/MacOS/"
cp "$repo_root/macOS/Info.plist" "$bundle_dir/Contents/Info.plist"
chmod +x "$bundle_dir/Contents/MacOS/BueroCockpit"

echo "$bundle_dir"
open "$bundle_dir"
