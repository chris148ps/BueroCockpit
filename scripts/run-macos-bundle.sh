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

mkdir -p "$publish_dir" "$bundle_dir/Contents/MacOS" "$bundle_dir/Contents/Resources"

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
codesign --force --deep --sign - "$bundle_dir"

echo "$bundle_dir"
open_args=()
if [[ -n "${BUEROCOCKPIT_DATA_DIRECTORY:-}" ]]; then
  open_args+=(--env "BUEROCOCKPIT_DATA_DIRECTORY=$BUEROCOCKPIT_DATA_DIRECTORY")
fi
if [[ -n "${BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY:-}" ]]; then
  open_args+=(--env "BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY=$BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY")
fi
if [[ -n "${BUEROCOCKPIT_DATA_DIRECTORY:-}" || -n "${BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY:-}" ]]; then
  open "${open_args[@]}" "$bundle_dir"
else
  open "$bundle_dir"
fi
