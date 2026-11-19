namespace Vid2Sub.Domain.Enums;

/// <summary>Subtitle file formats the tool can write.</summary>
public enum OutputFormat
{
    /// <summary>Plain text: one line per segment, no timing.</summary>
    Text,

    /// <summary>SubRip (.srt).</summary>
    Srt,

    /// <summary>WebVTT (.vtt).</summary>
    Vtt
}
