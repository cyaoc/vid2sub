## Agent skills

### Issue tracker

Issues live in GitHub Issues on cyaoc/vid2sub (via `gh`). See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one root `GLOSSARY.md` + `docs/adr/`. See `docs/agents/domain.md`.

# 项目约束

Vid2Sub 是 .NET 10 / C# 14 的命令行工具：视频或音频进，SRT / VTT / TXT 字幕出，识别引擎是 Whisper.net，识别结果可选用本机的 Claude Code 校正；已有的 SRT / VTT 也可以作为输入，只做校正。

## 完成标准

在仓库根目录运行，三条都通过才算完成：

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

- 改了 `src/vid2sub.csproj`、运行时包或打包脚本，再跑 `./scripts/build-portable.sh osx-arm64` 和 `win-x64`：原生库的目录布局只有发布才能验证。
- 改了音频解码、Whisper 调用，或校正的提示词（协议或 `correction-rules*.md`）和 `claude` 参数，用真实媒体文件跑一遍 CLI（校正加 `--glossary <用语集>`），单元测试覆盖不到这一段。没能验证的部分在汇报里写明。

## 结构

- 依赖只向内：`CLI → Application → Domain ← Infrastructure`，控制台输出只在 CLI。规则由 `tests/Vid2Sub.Tests/LayeringTests.cs` 强制执行，要改规则就改那份测试。
- 接口只在有第二个实现或测试需要替身时才加。
- 单元测试不访问网络，不加载真实 Whisper 模型或原生运行时，不启动 `claude`。替身入口是 `IAudioProcessor`、`ISpeechRecognizer`、`ISubtitleCorrector`，以及 `WhisperModelProvider` 的 `download` 参数和 `ClaudeCodeCorrector` 的 `run` 参数。

## 不变量

这六条没有测试守护，改相关代码时要保住。

- **硬件加速交给 Autoprobe**。Whisper.net 从引用的 Runtime 包里按 `CUDA > Vulkan > CoreML > OpenVINO > CPU` 自动选择。Windows 保留 `Whisper.net.Runtime.Vulkan`，AMD 和 Intel 显卡的用户靠它加速。
- **GPU 失败降级到 CPU**。`WhisperRecognizer` 在加速路径上遇到任何失败，都用 `UseGpu = false` 重试一次，并打印实际使用的运行时。
- **音频在进程内解码**。`ManagedAudioProcessor` 用纯托管的 ManagedCodecs，不调用 FFmpeg 或任何外部进程；缺某种格式就加托管解码器。理由和放弃的格式见 `docs/adr/0001-in-process-audio-decoding.md`。

- **校正走用户的 Claude Code 订阅**。用户有订阅、没有 API key，所以 `ClaudeCodeCorrector` 启动本机的 `claude -p`。保留 `--safe-mode`（`--bare` 只认 API key）和 `--tools ""`（模型只做文本转换，字幕内容无法让它执行操作）。模型只返回改动清单，时间轴由 `SegmentEditor` 计算。
- **校正时代码只测量，不判定**。多长算太长、多短算太碎写在各语言的规则文件里，是给模型的参考；代码把每条字幕的秒数和宽度量好交给模型，代码和 `config.yaml` 里都不放阈值，也不按数字强制拆分或合并。输入输出协议留在 `ClaudeCodeCorrector` 里，规则文字留在规则文件里，只写规则，不写给人看的说明（文件会原样发给模型）；通用规则在 `correction-rules.md`，长度和断点这类随语言而变的规则放在 `correction-rules.<语言代码>.md`，程序按字幕语言（设置优先，其次是 Whisper 识别出的）接在通用规则后面。
- **校正结果要有人复核**。模型的修改只是大概率正确，代码检查也认不出识别错了的术语，所以不追求全自动合格：每次校正都在字幕旁写出 `<name>.changes.txt`，按模型自报的依据把最可能出错的排在前面。改校正流程时保住这份清单，别用更多自动检查去替代人工复核。

Do not add any "Co-authored-by" trailers to git commit messages.
