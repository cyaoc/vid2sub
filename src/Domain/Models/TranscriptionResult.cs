namespace Vid2Sub.Domain.Models;

using Vid2Sub.Domain.Enums;

/// <summary>
/// 转录结果，包含源文件信息和所有转录片段
/// </summary>
/// <param name="SourceFile">源媒体文件路径</param>
/// <param name="Segments">转录片段集合</param>
/// <param name="Language">检测到的语言</param>
/// <param name="Duration">音频总时长</param>
public sealed record TranscriptionResult(
    string SourceFile,
    IReadOnlyList<TranscriptionSegment> Segments,
    TranscriptionStatus Status = TranscriptionStatus.Success,
    TranscriptionError? Error = null,
    string? Language = null,
    TimeSpan? Duration = null);
