namespace Vid2Sub.Domain.Models;

/// <summary>One recognized stretch of speech and when it is spoken.</summary>
public sealed record TranscriptionSegment(TimeSpan Start, TimeSpan End, string Text);
