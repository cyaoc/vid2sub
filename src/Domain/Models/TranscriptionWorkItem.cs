namespace Vid2Sub.Domain.Models;

public sealed record TranscriptionWorkItem(
    string SourcePath,
    string OutputPath);
