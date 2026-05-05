namespace Vid2Sub.Domain.Models;

/// <summary>
/// 转录片段，表示一段识别出的文本及其时间戳
/// </summary>
/// <param name="Start">片段开始时间</param>
/// <param name="End">片段结束时间</param>
/// <param name="Text">识别的文本内容</param>
public sealed record TranscriptionSegment(
    TimeSpan Start,
    TimeSpan End,
    string Text);
