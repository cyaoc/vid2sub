# FFmpeg Binaries for Portable Build

This directory contains platform-specific FFmpeg binaries for creating portable distributions.

## Directory Structure

```
vendor/ffmpeg/
├── osx-arm64/
│   └── ffmpeg
├── osx-x64/
│   └── ffmpeg
├── win-x64/
│   └── ffmpeg.exe
└── linux-x64/
    └── ffmpeg
```

## Download Instructions

### macOS (Intel & Apple Silicon)

Download from: https://evermeet.cx/ffmpeg/

1. Go to the website and download the latest **FFmpeg** snapshot (`.zip` version)
2. Extract the archive
3. Place the `ffmpeg` binary in the appropriate directory:
   - Apple Silicon: `vendor/ffmpeg/osx-arm64/ffmpeg`
   - Intel: `vendor/ffmpeg/osx-x64/ffmpeg`

**Note**: The binaries from evermeet.cx are Intel-only but run on Apple Silicon via Rosetta 2.
For native ARM64 builds, you may need to compile from source or use Homebrew.

```bash
# Remove macOS quarantine attribute
xattr -dr com.apple.quarantine vendor/ffmpeg/osx-arm64/ffmpeg
```

### Windows

Download from: https://www.gyan.dev/ffmpeg/builds/

1. Download `ffmpeg-release-essentials.zip` (smaller) or `ffmpeg-release-full.zip`
2. Extract and find `ffmpeg.exe` in the `bin/` folder
3. Place it at: `vendor/ffmpeg/win-x64/ffmpeg.exe`

### Linux

Download from: https://johnvansickle.com/ffmpeg/

1. Download the static build for your architecture (e.g., `ffmpeg-release-amd64-static.tar.xz`)
2. Extract the archive
3. Place the `ffmpeg` binary at: `vendor/ffmpeg/linux-x64/ffmpeg`

```bash
# Make executable
chmod +x vendor/ffmpeg/linux-x64/ffmpeg
```

## Usage

After placing the FFmpeg binary, run the build script:

```bash
# Build for current platform (default: osx-arm64)
./scripts/build-portable.sh

# Build for specific platform
./scripts/build-portable.sh osx-arm64
./scripts/build-portable.sh win-x64
./scripts/build-portable.sh linux-x64
```

## Notes

- FFmpeg binaries are **not** included in the git repository
- Each platform requires its own FFmpeg binary
- The build script will fail if the required FFmpeg binary is not found
