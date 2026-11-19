# Vid2Sub

A cross-platform video/audio to subtitle tool powered by Whisper.net.

## Features

- Reads MP4, MOV, M4V, MKV and AVI video and M4A, MP3, WAV and AAC audio, with AAC, MP3 or PCM sound
- Decodes media in-process: no FFmpeg or other external tool to install
- Writes SRT, VTT or plain-text subtitles
- Batch processing of files, directories and wildcard patterns
- Uses the best available hardware acceleration automatically (CUDA > Vulkan > CoreML > OpenVINO > CPU)
- YAML configuration file with command-line overrides
- Optional proofreading with the Claude Code CLI on your machine: fixes misrecognized terms against your glossary, removes lines Whisper invented, and re-cuts cues that are too long or too fragmented

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
  <input>  Media files, directories or wildcard patterns; an .srt or .vtt file is proofread without transcribing

Options:
  -o, --output-dir <dir>   Output directory (default: beside each input file)
  -f, --format <format>    Output format: srt, vtt, text
  -l, --language <lang>    Recognition language: auto, zh, en, ja, ...
  -m, --model <model>      Whisper model: Tiny, Base, Small, Medium, LargeV3, LargeV3Turbo
  -c, --config <path>      Config file path
  -t, --threads <num>      Processing threads (0 = one per CPU core)
  -v, --verbose            Print settings and every recognized segment
  -g, --glossary <file>    Proofread with the local Claude Code CLI, using this term list
  -G, --glossary-column <name>  Header of the glossary column in the language of the subtitles
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

correction:
  enabled: false                 # true proofreads every run; --glossary turns it on for one run
  model: "claude-opus-5-5"       # passed to `claude --model`
  effort: "medium"               # low, medium, high, xhigh, max: higher is slower and uses more of the usage limit
  glossary: "./glossary.xlsx"    # optional; relative paths are resolved from the application directory
  glossary_column: "ja"          # header of the column with the terms; not needed for a single-column glossary
```

### Proofreading with Claude Code

Give a glossary and the recognized text is proofread by the [Claude Code](https://claude.com/claude-code) CLI before the subtitle file is written. It runs on your Claude subscription, so no API key is needed: install Claude Code, sign in once, and make sure `claude` is on `PATH`.

```bash
vid2sub meeting.mp4 -f srt -g ./terms.xlsx -G ja
```

To proofread every run, set `correction.enabled: true` and `correction.glossary` in the config file instead; `--glossary` then swaps the term list for one run. A relative `--glossary` path is resolved from the current directory.

The glossary is an `.xlsx` workbook or a text file with one term per line. In a workbook the first row names the columns, and `-G` / `--glossary-column` (or `correction.glossary_column`) says which one holds the terms in the language of the subtitles: only that column is used, because translations do not help proofreading. A workbook with a single column needs no name. A cell with several lines counts as one term per line. The program prints what it read, for example `Glossary: 1576 terms from terms.xlsx, column "ja"`.

By default it changes three things and leaves every other word as spoken:

- **Misrecognized words.** The glossary lists the correct spellings of your names and terms; words that sound like one are rewritten to match.
- **Hallucinations.** Lines Whisper invented over silence or noise, such as a repeated phrase or "Thanks for watching".
- **Cue boundaries.** Fragments of one phrase are merged and overlong cues are split at the end of a clause. For Japanese the target is 30 full-width characters and 7 seconds per cue, with rules for where a cue may break.

The summary line reports what the result measures, for example `cue width 2-33 characters, longest 7.1 s`.

#### Reviewing the corrections

The corrections are likely, not certain, so plan for a person to check them. Beside every corrected subtitle file the program writes `<name>.changes.txt`: each correction with its time, the text before and after, grouped by the model's reason and ordered so the riskiest come first:

1. **Guesses**: a word changed on the model's own judgement of what was said
2. **Deletions**: text judged to be invented by the recognizer
3. **Terms**: rewritten to match the glossary
4. **Segmentation only**: cues merged, split or re-cut, words unchanged

The reason is the model's own and can be wrong; the list tells you where to look first, it does not replace reading the subtitles.

The list always describes the subtitles beside it: when a later run writes them without correction, because it is turned off or it failed, the list of the earlier run is removed.

When the review finds a name or term Whisper keeps getting wrong, add its correct spelling to the glossary so the next run fixes it.

#### Proofreading existing subtitles again

Name an `.srt` or `.vtt` file as the input and it is proofread without transcribing, which takes a minute or two instead of the whole run. Use it after changing the glossary or the rules:

```bash
vid2sub meeting.srt --glossary ./terms.xlsx -o ./corrected/
```

The result never overwrites an input, its own or another one of the same run, so give another folder with `-o` or another format with `-f`. Subtitle files are only taken when named; a directory or wildcard still picks up media files only.

#### Changing the rules

What to fix and what to leave alone, in any language, is written as plain prose in `correction-rules.md`, beside the executable. How long a cue should be and where it may break depends on the language, so that is in `correction-rules.<language>.md` (`correction-rules.ja.md` ships with the program). The program appends the file of the subtitles' language to the common rules, so the model only reads the rules it needs.

Edit the files in any text editor and in any language; the next run uses them. They are sent to the model exactly as they are, so write only rules in them, not notes for people. Delete a file to go back to its built-in version. To cover another language, add a file named after its code, for example `correction-rules.zh.md`; until then, subtitles in that language are proofread by the common rules alone, which set no target length.

The language is the one given with `-l` or `inference.language`; with `auto` it is the language Whisper recognized. An existing subtitle file proofread with `auto` has no known language and gets the common rules only, so name the language for those. Each run prints which rules it used, for example `Correction rules: correction-rules.md + correction-rules.ja.md`.

The numbers in those files are guidance for the model, not limits the program enforces: the program only measures each cue (seconds shown, and width with half-width letters counted as half) and hands the numbers to the model, which decides where a cue reads best.

Good to know:

- The transcript and the glossary are sent to Anthropic through your Claude account and count against its usage limits.
- If Claude Code is not installed or not signed in, the run stops with an error before transcribing anything.
- If the correction fails later (usage limit, network, timeout), the subtitles are still written, as recognized, and that file is reported as failed.
- Merged cues keep exact timing. Split cues share the original time span in proportion to their length, so their boundaries are estimates.

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
│   ├── Infrastructure/   # Audio decoding, Whisper, model download, Claude Code correction, YAML config
│   └── CLI/              # Command-line entry point
├── tests/Vid2Sub.Tests/  # Unit tests
├── scripts/              # Portable build script
├── config.yaml           # Default config file
├── correction-rules.md      # Default proofreading rules, shipped beside the executable and embedded as the fallback
└── correction-rules.ja.md   # Rules for Japanese subtitles, appended when the subtitles are Japanese
```

Contributor and agent rules are in [AGENTS.md](AGENTS.md).

## License

MIT License
