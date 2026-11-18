---
status: accepted
---

# 音频在进程内用托管代码解码，不依赖 FFmpeg

此前 vid2sub 启动 `ffmpeg` 子进程，把输入转成 16 kHz 单声道的 WAV 临时文件；打便携包要先手动把 FFmpeg 下载到 `vendor/`，再随包分发。2026-11 起改为在进程内解码：`ManagedAudioProcessor` 用纯托管库 ManagedCodecs 解封装和解码，再由自己的重采样器和声道混合输出 16 kHz 单声道采样。这样发布包里没有外部可执行文件，运行时不起子进程、不写临时文件，也和已经不用 FFmpeg 的姊妹项目 cyaoc/subtitles（Rust + Symphonia）保持一致。代价是支持的格式比 FFmpeg 少，我们接受这一点。

## Considered Options

- **保留 ffmpeg 子进程，由 CI 下载并打包。** C# 代码几乎不用改，格式最全。发布包里仍然带着一个 FFmpeg 二进制，没有达到去掉它的目的。
- **进程内调用 libav（FFmpeg.AutoGen）。** 本质还是 FFmpeg，原生库仍要随包携带；NuGet 上现成的运行时包只找到 windows-x64。
- **拼装多个纯托管解码器（NLayer、NVorbis、Concentus、SharpJaad.AAC）。** 容器解析要自己写；AAC 是视频里最常见的音轨，而它的解码库还在 0.2.0。
- **用 Rust 写原生库，复用 subtitles 的 Symphonia。** 格式覆盖能和 subtitles 相同，但 .NET 项目要多一条 Rust 工具链，每个平台还要各自构建并随包携带原生库。
- **ManagedCodecs（采用）。** 一个库同时提供容器解析和 AAC、MP3、PCM 解码，纯托管、MIT 许可。原型在原始采样率下的输出与 ffmpeg 逐采样一致，最大偏差在 1e-7 量级。

## Consequences

- **支持的格式变少。** 实测可用：MP4、M4V、M4A、MKV（AAC 音轨）、ADTS AAC、AVI（MP3 音轨）、MOV（PCM 音轨）、MP3、WAV，以及 5.1 声道的 AAC。不支持并明确报错：FLAC、OGG/Vorbis、Opus（含 WebM）、ALAC、AC-3、DTS、WMV/WMA、FLV。其中 FLAC、Vorbis、ALAC 是 subtitles 支持的。HE-AAC 没有测过。
- **缺某种格式时加托管解码器**，不退回外部工具。
- **ManagedCodecs 很新。** 仓库 2026-10-20 创建，单人维护，采用时只有 3 个 star。版本锁在 0.18.0，只有 `ManagedAudioProcessor` 一个类引用它，要弃用时换掉这个类即可。
- **重采样和声道混合是自己的代码**（`Resampler`，混合权重移植自 subtitles）。完整链路与 `ffmpeg -ar 16000 -ac 1` 对比：一段 19.7 分钟的视频采样数相同，相关系数 0.999997，信噪比 53 dB。
- **整段音频解码进内存**，每小时约 230 MB。几小时长的文件没有实测。
