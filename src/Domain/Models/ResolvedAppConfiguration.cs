using Vid2Sub.Domain.Enums;

namespace Vid2Sub.Domain.Models;

public sealed record ResolvedAppConfiguration(
    ResolvedModelConfiguration Model,
    ResolvedInferenceConfiguration Inference,
    ResolvedEnvironmentConfiguration Environment,
    ResolvedOutputConfiguration Output);

public sealed record ResolvedModelConfiguration(
    ModelType Type,
    string StorageDir);

public sealed record ResolvedInferenceConfiguration(
    string Language,
    int Threads,
    int BeamSize)
{
    public int EffectiveThreads => Threads <= 0 ? Environment.ProcessorCount : Threads;
}

public sealed record ResolvedEnvironmentConfiguration(
    string FfmpegPath,
    string TempDir);

public sealed record ResolvedOutputConfiguration(
    OutputFormat Format,
    Vid2SubLogLevel LogLevel,
    string? OutputDir,
    bool Overwrite);
