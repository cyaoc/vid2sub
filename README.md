# Vid2Sub

A cross-platform video/audio to subtitle tool powered by Whisper.net.

## Features

- Supports multiple video formats: MP4, MKV, AVI, MOV, WMV, FLV, WebM
- Supports multiple audio formats: MP3, WAV, FLAC, AAC, OGG, M4A, WMA
- Supports multiple subtitle output formats: SRT, VTT, TXT
- Batch processing of files and directories
- Auto-detection and use of best hardware acceleration (CUDA > Vulkan > CoreML > CPU)
- Flexible YAML configuration file with command-line overrides

## Requirements

### Runtime
- .NET 10 Runtime (only required for framework-dependent builds; self-contained builds include the runtime)

### External Tools
- FFmpeg (must be in PATH or specify path in config file)

### Hardware Drivers (Optional, for GPU acceleration)
- **Windows**: Nvidia CUDA driver or Vulkan driver (supports AMD/Intel integrated graphics)
- **macOS**: Apple Silicon (automatically uses CoreML)
- **Linux**: CUDA driver

## Installation

### From Source

```bash
# Clone the repository
git clone https://github.com/your-repo/vid2sub.git
cd vid2sub

# Restore dependencies
dotnet restore
```

### Build Options

#### 1. Framework-dependent Build (Smaller size, requires .NET Runtime)

```bash
dotnet publish -c Release
```

Output: `bin/Release/net10.0/publish/`

#### 2. Self-contained Build (No runtime required)

```bash
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained

# macOS Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained

# macOS Intel
dotnet publish -c Release -r osx-x64 --self-contained

# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained
```

Output: `bin/Release/net10.0/<runtime-identifier>/publish/vid2sub` (single executable)

**Optimizations enabled by default in Release mode:**
- **Single-file**: Packages everything into one executable
- **Compression**: Compresses the single-file output
- **Platform-specific runtime**: Only includes native libraries for the target platform

| Platform | Runtime | Acceleration |
|----------|---------|--------------|
| macOS | CoreML | Apple Neural Engine |
| Windows | Cuda、Vulkan | AMD/Intel/NVIDIA GPU |
| Linux | CPU | Multi-threaded CPU |

These optimizations significantly reduce the output size compared to a standard self-contained build.

## Usage

### Basic Usage

```bash
# Process a single file
vid2sub video.mp4

# Specify output format
vid2sub video.mp4 -f srt

# Specify output directory
vid2sub video.mp4 -o ./subtitles/

# Process entire directory
vid2sub ./videos/

# Batch process multiple files
vid2sub video1.mp4 video2.mp4 video3.mp4
```

### Command-line Arguments

```
vid2sub <input> [options]

Arguments:
  <input>  Input file(s) or directory path (supports multiple)

Options:
  -o, --output-dir <dir>   Output directory (default: same as input file)
  -f, --format <format>    Output format: srt, vtt, text
  -l, --language <lang>    Recognition language: auto, zh, en, ja, etc.
  -m, --model <type>       Model type: Tiny, Base, Small, Medium, LargeV3, LargeV3Turbo
  -c, --config <path>      Specify config file path
  -t, --threads <num>      Number of processing threads
  -v, --verbose            Verbose output mode
  --help                   Show help information
```

### Configuration File

The program searches for `config.yaml` in the following order:
1. Current working directory
2. Application directory

Example configuration (`config.yaml`):

```yaml
# Model Configuration
model:
  type: "Medium"           # Tiny, Base, Small, Medium, LargeV3, LargeV3Turbo
  storage_dir: "./models"

# Inference Parameters
inference:
  language: "auto"         # auto, zh, en, ja, etc.
  threads: 0               # 0 = auto-detect CPU cores
  beam_size: 5

# External Environment Configuration
environment:
  ffmpeg_path: "ffmpeg"
  temp_dir: "./temp"

# Output Settings
output:
  format: "vtt"            # text, srt, vtt
  verbose: true
```

### Configuration Priority

Command-line arguments > YAML config file > Default values

## Project Structure

```
vid2sub/
├── src/
│   ├── Domain/           # Domain layer: interfaces, models, enums
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   └── Enums/
│   ├── Infrastructure/   # Infrastructure layer: implementations
│   │   ├── Audio/        # FFmpeg audio processing
│   │   ├── Models/       # Whisper model management
│   │   ├── Subtitles/    # Subtitle writers
│   │   └── Configuration/
│   ├── Application/      # Application layer: business orchestration
│   └── CLI/              # Command-line entry point
├── config.yaml           # Default config file
└── vid2sub.csproj
```

## Model Information

Models are automatically downloaded from Hugging Face on first run. Model sizes and accuracy:

| Model | Size | Use Case |
|-------|------|----------|
| Tiny | ~75 MB | Quick testing, lower accuracy |
| Base | ~142 MB | Daily use, balanced speed and accuracy |
| Small | ~466 MB | Higher accuracy |
| Medium | ~1.5 GB | Recommended, high accuracy |
| LargeV3 | ~3 GB | Highest accuracy, requires more resources |
| LargeV3Turbo | ~1.6 GB | High accuracy and fast, recommended |

## License

MIT License
