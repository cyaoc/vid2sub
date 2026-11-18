#!/bin/bash
# Builds a self-contained portable distribution in dist/vid2sub-<platform>.
# Usage: ./scripts/build-portable.sh [osx-arm64|osx-x64|win-x64|linux-x64]
set -euo pipefail

PLATFORM="${1:-osx-arm64}"
case "$PLATFORM" in
    osx-arm64|osx-x64|win-x64|linux-x64) ;;
    *) echo "Unsupported platform '$PLATFORM' (use osx-arm64, osx-x64, win-x64 or linux-x64)" >&2; exit 1 ;;
esac

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST="$ROOT/dist/vid2sub-$PLATFORM"
NATIVE="${PLATFORM/osx/macos}" # Whisper.net names its macOS runtime folders macos-*

rm -rf "$DIST"
dotnet publish "$ROOT/src" -c Release -r "$PLATFORM" --self-contained -o "$DIST" -v quiet
cp "$ROOT/config.yaml" "$DIST/"
mkdir -p "$DIST/models"

# Whisper.net ships native libraries for every platform; keep only the target's.
for dir in "$DIST"/runtimes/* "$DIST"/runtimes/*/*; do
    [ -d "$dir" ] || continue
    case "$(basename "$dir")" in
        "$NATIVE"|cuda|vulkan|openvino|coreml) ;;
        *) rm -rf "$dir" ;;
    esac
done
find "$DIST/runtimes" -type d -empty -delete
rm -f "$DIST/ggml-metal.metal" # the Metal shader is already compiled into the dylib

echo "Built $DIST"
echo "Archive it with: (cd \"$ROOT/dist\" && zip -r vid2sub-$PLATFORM.zip vid2sub-$PLATFORM)"
