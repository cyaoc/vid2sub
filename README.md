# Vid2Sub

A cross-platform video/audio to subtitle tool powered by Whisper.net.

## Features

- Supports multiple video formats: MP4, MKV, AVI, MOV, WMV, FLV, WebM
- Supports multiple audio formats: MP3, WAV, FLAC, AAC, OGG, M4A, WMA
- Supports multiple subtitle output formats: SRT, VTT, TXT
- Batch processing of files and directories
- Auto-detection and use of best hardware acceleration (CUDA > Vulkan > CoreML > CPU)
- Flexible YAML configuration file with command-line overrides

## Quick Start (Portable Version)

Pre-built portable versions are available for immediate use:

| Platform | Download | Included Acceleration |
|----------|----------|----------------------|
| **macOS Apple Silicon** | [vid2sub-osx-arm64.zip](https://github.com/cyaoc/vid2sub/releases/latest) | CoreML (Apple Neural Engine) |
| **Windows x64** | [vid2sub-win-x64.zip](https://github.com/cyaoc/vid2sub/releases/latest) | CUDA, Vulkan, OpenVino, CPU |

Download, extract, and run! FFmpeg is bundled in the portable version.

## Requirements

### For Portable Version (Windows)

**Required:**
- Windows 11 or Windows Server 2022 (or newer)
- [Microsoft Visual C++ Redistributable 2022 (x64)](https://aka.ms/vc14/vc_redist.x64.exe)

**Optional GPU Acceleration:**

| Acceleration | Requirement | Download |
|--------------|-------------|----------|
| **CUDA** | NVIDIA GPU + CUDA Toolkit >= 13.0 | [CUDA Toolkit](https://developer.nvidia.com/cuda-downloads) |
| **Vulkan** | AMD/Intel/NVIDIA GPU + Vulkan driver | Usually included with graphics drivers |
| **OpenVino** | Intel CPU/GPU + OpenVino >= 2024.4 | [OpenVino Toolkit](https://www.intel.com/content/www/us/en/developer/tools/openvino-toolkit/download.html) |

> Note: If no GPU acceleration is available, the program automatically falls back to CPU mode.

### For Portable Version (macOS)

- macOS with Apple Silicon (M1/M2/M3/M4)
- CoreML acceleration is automatically enabled

### For Building from Source

**Runtime:**
- .NET 10 Runtime (only required for framework-dependent builds; self-contained builds include the runtime)

**External Tools:**
- FFmpeg (must be in PATH or specify path in config file)

**Hardware Drivers (Optional):**
- **Windows**: NVIDIA CUDA driver or Vulkan driver (supports AMD/Intel integrated graphics)
- **macOS**: Apple Silicon (automatically uses CoreML)
- **Linux**: CUDA driver

## Installation

### From Source

```bash
# Clone the repository
git clone https://github.com/cyaoc/vid2sub.git
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

### Testing

```bash
# Build and run unit tests
./scripts/test.sh

# Full pre-ship verification, including publish checks
./scripts/test.sh --full
```

## Codex Workflow MCP Server

Vid2Sub also includes an optional local Codex workflow layer for subtitle correction and translation with an Excel glossary.

### What It Does

The Codex workflow is for this local subtitle pipeline:

1. Generate raw Japanese subtitles from a media file with `vid2sub`.
2. Read a `.xlsx` glossary or correction workbook.
3. Correct the Japanese subtitle text using the glossary.
4. Translate the corrected Japanese subtitles into Chinese.
5. Write delivery files plus a workflow manifest and glossary audit.

Expected outputs:

```text
{inputName}.raw.ja.srt
{inputName}.corrected.ja.srt
{inputName}.translated.zh.srt
workflow.manifest.json
glossary-audit.json
```

### Build The Tools

Build the main `vid2sub` CLI:

```bash
dotnet publish -c Release
```

Build the workflow MCP server:

```bash
dotnet publish tools/Vid2Sub.WorkflowMcp/Vid2Sub.WorkflowMcp.csproj -c Release
```

### Configure Codex

Add the published MCP server to your Codex MCP configuration. Use absolute paths.

```toml
[mcp_servers.vid2sub-workflow]
command = "/absolute/path/to/vid2sub/tools/Vid2Sub.WorkflowMcp/bin/Release/net10.0/publish/vid2sub-workflow-mcp"
args = []
```

Install or symlink the skill directory into your Codex skills directory:

```text
skills/vid2sub-workflow/
```

### Run The Workflow

In Codex, invoke the skill explicitly:

```text
$vid2sub-workflow
```

Then provide:

- the media file path
- the `.xlsx` glossary path
- the output directory
- the published `vid2sub` executable path

The skill will inspect the workbook, ask you to confirm the sheet/column mapping when needed, show the output files before writing, and use the MCP tools to read/write subtitles safely inside the confirmed output directory.

### Notes

- The workflow uses `.xlsx` glossaries through ClosedXML.
- It does not overwrite existing output files unless you explicitly confirm overwrite.
- It keeps subtitle timestamps unchanged during correction and translation unless you ask for retiming, splitting, or merging.
- Installation and smoke-test details are in `docs/vid2sub-workflow-install.md`.
- Prompt/eval cases for glossary behavior are in `docs/vid2sub-workflow-evals.md`.

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
  --log-level <level>      Log level: quiet, error, warning, information, debug
  --overwrite              Overwrite existing subtitle files
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
  log_level: "information" # quiet, error, warning, information, debug
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
├── tools/
│   └── Vid2Sub.WorkflowMcp/  # Local MCP server for Codex workflow tools
├── skills/
│   └── vid2sub-workflow/     # Codex skill for glossary-assisted subtitle workflow
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
