namespace Vid2Sub.Domain.Models;

public sealed record CliOptions(
    IReadOnlyList<string> Inputs,
    string? OutputDir = null,
    string? Format = null,
    string? Language = null,
    string? Model = null,
    string? ConfigPath = null,
    int? Threads = null,
    string? LogLevel = null,
    bool Overwrite = false);
