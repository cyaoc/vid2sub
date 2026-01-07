#!/bin/bash

# =============================================================================
# Vid2Sub Portable Build Script
# =============================================================================
# Usage: ./scripts/build-portable.sh [platform]
# 
# Platforms:
#   osx-arm64   - macOS Apple Silicon (default)
#   win-x64     - Windows x64
# =============================================================================

set -e

# Script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default platform
PLATFORM="${1:-osx-arm64}"

# Validate platform
case "$PLATFORM" in
    osx-arm64|osx-x64|win-x64|linux-x64)
        ;;
    *)
        echo "Error: Unsupported platform '$PLATFORM'"
        echo "Supported platforms: osx-arm64, osx-x64, win-x64, linux-x64"
        exit 1
        ;;
esac

# Determine ffmpeg executable name
if [[ "$PLATFORM" == win-* ]]; then
    FFMPEG_EXE="ffmpeg.exe"
else
    FFMPEG_EXE="ffmpeg"
fi

# Paths
VENDOR_FFMPEG="$PROJECT_ROOT/vendor/ffmpeg/$PLATFORM/$FFMPEG_EXE"
DIST_DIR="$PROJECT_ROOT/dist/vid2sub-$PLATFORM"
CONFIG_SRC="$PROJECT_ROOT/config.yaml"

echo "=========================================="
echo "Vid2Sub Portable Build"
echo "=========================================="
echo "Platform: $PLATFORM"
echo "Output:   $DIST_DIR"

# Display included runtimes based on platform
case "$PLATFORM" in
    osx-*)
        echo "Runtimes: CoreML + CPU"
        ;;
    win-*)
        echo "Runtimes: CUDA + Vulkan + OpenVino + CPU (auto-detect)"
        echo "  Note: CUDA requires NVIDIA CUDA Toolkit 13.0+"
        ;;
    linux-*)
        echo "Runtimes: CPU"
        ;;
esac
echo ""

# Check if ffmpeg exists
if [[ ! -f "$VENDOR_FFMPEG" ]]; then
    echo "Error: FFmpeg not found at $VENDOR_FFMPEG"
    echo ""
    echo "Please download ffmpeg for $PLATFORM and place it at:"
    echo "  $VENDOR_FFMPEG"
    echo ""
    echo "See vendor/ffmpeg/README.md for download instructions."
    exit 1
fi

# Clean previous build
echo "[1/6] Cleaning previous build..."
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR"

# Build with dotnet publish
echo "[2/6] Building for $PLATFORM..."
cd "$PROJECT_ROOT"
dotnet publish -c Release -r "$PLATFORM" --self-contained -o "$DIST_DIR" -v quiet

# Create bin directory and copy ffmpeg
echo "[3/6] Copying FFmpeg..."
mkdir -p "$DIST_DIR/bin"
cp "$VENDOR_FFMPEG" "$DIST_DIR/bin/"

# Make ffmpeg executable (Unix only)
if [[ "$PLATFORM" != win-* ]]; then
    chmod +x "$DIST_DIR/bin/$FFMPEG_EXE"
fi

# Copy and modify config.yaml
echo "[4/6] Creating config.yaml..."
if [[ -f "$CONFIG_SRC" ]]; then
    # Copy config and modify ffmpeg_path
    if [[ "$PLATFORM" == win-* ]]; then
        # Windows: use backslash or forward slash both work
        sed 's|ffmpeg_path: "ffmpeg"|ffmpeg_path: "./bin/ffmpeg.exe"|g' "$CONFIG_SRC" > "$DIST_DIR/config.yaml"
    else
        sed 's|ffmpeg_path: "ffmpeg"|ffmpeg_path: "./bin/ffmpeg"|g' "$CONFIG_SRC" > "$DIST_DIR/config.yaml"
    fi
else
    echo "Warning: config.yaml not found, skipping..."
fi

# Create models directory
echo "[5/6] Creating models directory..."
mkdir -p "$DIST_DIR/models"

# Clean up unnecessary runtime directories and files
echo "[6/6] Cleaning up unnecessary files..."
case "$PLATFORM" in
    osx-arm64)
        # macOS ARM64: only keep runtimes/coreml/macos-arm64/
        # Native libs are in runtimes/, NOT embedded in executable
        rm -rf "$DIST_DIR/runtimes/linux-"* 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/win-"* 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/macos-x64" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/coreml/macos-x64" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/cuda" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/vulkan" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/openvino" 2>/dev/null || true
        echo "  Kept: runtimes/coreml/macos-arm64/"
        ;;
    osx-x64)
        # macOS x64: only keep runtimes/coreml/macos-x64/
        rm -rf "$DIST_DIR/runtimes/linux-"* 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/win-"* 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/macos-arm64" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/coreml/macos-arm64" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/cuda" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/vulkan" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/openvino" 2>/dev/null || true
        echo "  Kept: runtimes/coreml/macos-x64/"
        ;;
    win-x64)
        # Windows x64: ALL native DLLs are embedded in exe (IncludeNativeLibrariesForSelfExtract=true)
        # runtimes/ directory only contains Linux/macOS files, can be removed entirely
        rm -rf "$DIST_DIR/runtimes" 2>/dev/null || true
        echo "  Removed: runtimes/ (DLLs embedded in exe)"
        ;;
    linux-x64)
        # Linux x64: keep runtimes/linux-x64/ only
        rm -rf "$DIST_DIR/runtimes/macos-"* 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/win-"* 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/coreml" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/linux-arm"* 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/cuda" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/vulkan" 2>/dev/null || true
        rm -rf "$DIST_DIR/runtimes/openvino" 2>/dev/null || true
        echo "  Kept: runtimes/linux-x64/"
        ;;
esac

# Remove ggml-metal.metal (macOS Metal shader, not needed - compiled into dylib)
rm -f "$DIST_DIR/ggml-metal.metal" 2>/dev/null || true

# Summary
echo ""
echo "=========================================="
echo "Build Complete!"
echo "=========================================="
echo ""
echo "Output directory: $DIST_DIR"
echo ""

# Display runtime information
case "$PLATFORM" in
    osx-*)
        echo "Included Runtimes:"
        echo "  - CoreML (Apple Neural Engine, auto-enabled)"
        echo "  - CPU (fallback)"
        echo ""
        ;;
    win-*)
        echo "Included Runtimes (auto-detect order):"
        echo "  1. CUDA     - Requires NVIDIA CUDA Toolkit 13.0+"
        echo "  2. Vulkan   - Works with most GPUs (AMD/Intel/NVIDIA)"
        echo "  3. OpenVino - Requires Intel OpenVino 2024.4+"
        echo "  4. CPU      - Always available (fallback)"
        echo ""
        ;;
    linux-*)
        echo "Included Runtimes:"
        echo "  - CPU"
        echo ""
        ;;
esac

echo "Contents:"
ls -la "$DIST_DIR"
echo ""
echo "To create a distributable archive:"
echo "  cd $PROJECT_ROOT/dist"
echo "  zip -r vid2sub-$PLATFORM.zip vid2sub-$PLATFORM"
echo ""

# macOS specific note
if [[ "$PLATFORM" == osx-* ]]; then
    echo "Note for macOS users:"
    echo "  If you see security warnings, run:"
    echo "  xattr -dr com.apple.quarantine $DIST_DIR"
    echo ""
fi

# Windows specific note
if [[ "$PLATFORM" == win-* ]]; then
    echo "Note for Windows users:"
    echo "  - Vulkan/CPU works out-of-the-box with modern graphics drivers"
    echo "  - For CUDA acceleration: Install NVIDIA CUDA Toolkit"
    echo "  - For OpenVino acceleration: Install Intel OpenVino Runtime"
    echo ""
fi
