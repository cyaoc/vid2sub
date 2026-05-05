namespace Vid2Sub.Domain.Models;

public enum TranscriptionProgressKind
{
    BatchStarted,
    RuntimeReady,
    FileStarted,
    FileCompleted,
    FileFailed
}

public sealed record TranscriptionProgressEvent(
    TranscriptionProgressKind Kind,
    string Message,
    string? SourceFile = null);
