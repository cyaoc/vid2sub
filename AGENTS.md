## Agent skills

### Issue tracker

Issues live in GitHub Issues on cyaoc/vid2sub (via `gh`). See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one root `GLOSSARY.md` + `docs/adr/`. See `docs/agents/domain.md`.

# 项目约束

Vid2Sub 是 .NET 10 / C# 14 的命令行工具：视频或音频进，SRT / VTT / TXT 字幕出，识别引擎是 Whisper.net。

## 完成标准

在仓库根目录运行，三条都通过才算完成：

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

- 改了 `src/vid2sub.csproj`、运行时包或打包脚本，再跑 `./scripts/build-portable.sh osx-arm64` 和 `win-x64`：原生库的目录布局只有发布才能验证。
- 改了音频解码或 Whisper 调用，用真实媒体文件跑一遍 CLI，单元测试覆盖不到这一段。没能验证的部分在汇报里写明。

## 结构

- 依赖只向内：`CLI → Application → Domain ← Infrastructure`，控制台输出只在 CLI。规则由 `tests/Vid2Sub.Tests/LayeringTests.cs` 强制执行，要改规则就改那份测试。
- 接口只在有第二个实现或测试需要替身时才加。
- 单元测试不访问网络，不加载真实 Whisper 模型或原生运行时。替身入口是 `IAudioProcessor`、`ISpeechRecognizer` 和 `WhisperModelProvider` 的 `download` 参数。

## 不变量

这三条没有测试守护，改相关代码时要保住。

- **硬件加速交给 Autoprobe**。Whisper.net 从引用的 Runtime 包里按 `CUDA > Vulkan > CoreML > OpenVINO > CPU` 自动选择。Windows 保留 `Whisper.net.Runtime.Vulkan`，AMD 和 Intel 显卡的用户靠它加速。
- **GPU 失败降级到 CPU**。`WhisperRecognizer` 在加速路径上遇到任何失败，都用 `UseGpu = false` 重试一次，并打印实际使用的运行时。
- **音频在进程内解码**。`ManagedAudioProcessor` 用纯托管的 ManagedCodecs，不调用 FFmpeg 或任何外部进程；缺某种格式就加托管解码器。理由和放弃的格式见 `docs/adr/0001-in-process-audio-decoding.md`。

Do not add any "Co-authored-by" trailers to git commit messages.
