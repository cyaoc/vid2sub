# Vid2Sub

A cross-platform video/audio to subtitle tool powered by Whisper.net.

## Features

- Reads MP4, MOV, M4V, MKV and AVI video and M4A, MP3, WAV and AAC audio, with AAC, MP3 or PCM sound
- Decodes media in-process: no FFmpeg or other external tool to install
- Writes SRT, VTT or plain-text subtitles
- Batch processing of files, directories and wildcard patterns
- Uses the best available hardware acceleration automatically (CUDA > Vulkan > CoreML > OpenVINO > CPU)
- YAML configuration file with command-line overrides

Not supported: FLAC, OGG/Vorbis, Opus (including WebM), AC-3, DTS, WMV/WMA and FLV. Convert those to one of the formats above first.

## Quick Start (Portable Version)

Pre-built portable versions are available for immediate use:

| Platform | Download | Included Acceleration |
|----------|----------|----------------------|
| **macOS Apple Silicon** | [vid2sub-osx-arm64.zip](https://github.com/cyaoc/vid2sub/releases/latest) | CoreML (Apple Neural Engine) |
| **Windows x64** | [vid2sub-win-x64.zip](https://github.com/cyaoc/vid2sub/releases/latest) | CUDA, Vulkan, OpenVINO, CPU |

Download, extract, and run. Nothing else needs to be installed.

## Requirements

### Portable Version (Windows)

**Required:**
- Windows 11 or Windows Server 2022 (or newer)
- [Microsoft Visual C++ Redistributable 2022 (x64)](https://aka.ms/vc14/vc_redist.x64.exe)

**Optional GPU Acceleration:**

| Acceleration | Requirement | Download |
|--------------|-------------|----------|
| **CUDA** | NVIDIA GPU + CUDA Toolkit >= 13.0 | [CUDA Toolkit](https://developer.nvidia.com/cuda-downloads) |
| **Vulkan** | AMD/Intel/NVIDIA GPU + Vulkan driver | Usually included with graphics drivers |
| **OpenVINO** | Intel CPU/GPU + OpenVINO >= 2024.4 | [OpenVINO Toolkit](https://www.intel.com/content/www/us/en/developer/tools/openvino-toolkit/download.html) |

> If no GPU acceleration is available, or the GPU fails during a run, the program falls back to CPU.

### Portable Version (macOS)

- macOS with Apple Silicon (M1 or newer)
- CoreML acceleration is enabled automatically
- If macOS blocks the download, run `xattr -dr com.apple.quarantine vid2sub-osx-arm64`

### Building from Source

- .NET 10 SDK

## Usage

```bash
# Process a single file
vid2sub video.mp4

# Choose the output format and directory
vid2sub video.mp4 -f srt -o ./subtitles/

# Process every media file in a directory
vid2sub ./videos/

# Several files or a wildcard pattern
vid2sub video1.mp4 video2.mp4 "lectures/week*.mp4"
```

```
vid2sub <input>... [options]

Arguments:
  <input>  Input files, directories or wildcard patterns

Options:
  -o, --output-dir <dir>   Output directory (default: beside each input file)
  -f, --format <format>    Output format: srt, vtt, text
  -l, --language <lang>    Recognition language: auto, zh, en, ja, ...
  -m, --model <model>      Whisper model: Tiny, Base, Small, Medium, LargeV3, LargeV3Turbo
  -c, --config <path>      Config file path
  -t, --threads <num>      Processing threads (0 = one per CPU core)
  -v, --verbose            Print settings and every recognized segment
  -h, --help               Show help
```

The exit code is 0 when every file succeeded and 1 when any failed.

### Configuration File

Settings are taken in this order: command-line arguments, then the config file, then built-in defaults.

Without `--config`, the program reads `config.yaml` from the current working directory, then from the application directory.

```yaml
model:
  type: "LargeV3Turbo"     # Tiny, Base, Small, Medium, LargeV3, LargeV3Turbo
  storage_dir: "./models"  # relative paths are resolved from the application directory

inference:
  language: "auto"         # auto, zh, en, ja, ...
  threads: 0               # 0 = one per CPU core
  beam_size: 5             # higher is more accurate but slower

output:
  format: "vtt"            # text, srt, vtt
  verbose: true
```

### Models

Models are downloaded from Hugging Face on first use and cached in `storage_dir`.

| Model | Size | Use Case |
|-------|------|----------|
| Tiny | ~75 MB | Quick testing, lower accuracy |
| Base | ~142 MB | Daily use, balanced speed and accuracy |
| Small | ~466 MB | Higher accuracy |
| Medium | ~1.5 GB | High accuracy |
| LargeV3 | ~3 GB | Highest accuracy, requires more resources |
| LargeV3Turbo | ~1.6 GB | High accuracy and fast, the default |

## Development

```bash
git clone https://github.com/cyaoc/vid2sub.git
cd vid2sub

dotnet build                       # build everything
dotnet test                        # run the unit tests
dotnet run --project src -- video.mp4

# Self-contained portable build in dist/vid2sub-<platform>
./scripts/build-portable.sh osx-arm64   # or osx-x64, win-x64, linux-x64
```

```
vid2sub/
├── src/
│   ├── Domain/           # Models, enums, interfaces, subtitle formatting
│   ├── Application/      # The transcription use case
│   ├── Infrastructure/   # Audio decoding, Whisper, model download, YAML config
│   └── CLI/              # Command-line entry point
├── tests/Vid2Sub.Tests/  # Unit tests
├── scripts/              # Portable build script
└── config.yaml           # Default config file
```

Contributor and agent rules are in [AGENTS.md](AGENTS.md).

## License

MIT License
