namespace Vid2Sub.Domain.Enums;

/// <summary>
/// 字幕输出格式
/// </summary>
public enum OutputFormat
{
    /// <summary>
    /// 纯文本格式
    /// </summary>
    Text,
    
    /// <summary>
    /// SubRip 字幕格式 (.srt)
    /// </summary>
    Srt,
    
    /// <summary>
    /// WebVTT 字幕格式 (.vtt)
    /// </summary>
    Vtt
}
