namespace Vid2Sub.Domain.Models;

/// <summary>
/// One recognized stretch of speech and when it is spoken. <paramref name="Language"/> is the code Whisper
/// reports for it ("ja", "en", ...); it is unknown for segments that were read from a subtitle file or edited.
/// </summary>
public sealed record TranscriptionSegment(TimeSpan Start, TimeSpan End, string Text, string? Language = null);
