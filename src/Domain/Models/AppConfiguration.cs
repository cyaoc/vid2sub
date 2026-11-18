using Vid2Sub.Domain.Enums;

namespace Vid2Sub.Domain.Models;

/// <summary>
/// Mirrors config.yaml (snake_case keys). Property initializers are the defaults;
/// precedence is command line > YAML > defaults.
/// </summary>
public sealed class AppConfiguration
{
    public ModelConfig Model { get; set; } = new();
    public InferenceConfig Inference { get; set; } = new();
    public OutputConfig Output { get; set; } = new();

    /// <summary>Applies the command-line values that were actually given, then validates the result.</summary>
    public void ApplyOverrides(
        OutputFormat? format = null,
        string? language = null,
        ModelType? model = null,
        int? threads = null,
        bool verbose = false)
    {
        Output.Format = format ?? Output.Format;
        Output.Verbose |= verbose;
        Inference.Language = string.IsNullOrWhiteSpace(language) ? Inference.Language : language;
        Inference.Threads = threads ?? Inference.Threads;
        Model.Type = model ?? Model.Type;

        if (Inference.Threads < 0)
        {
            throw new ArgumentException($"threads must be 0 (auto) or greater, got {Inference.Threads}.");
        }

        if (Inference.BeamSize < 1)
        {
            throw new ArgumentException($"beam_size must be 1 or greater, got {Inference.BeamSize}.");
        }
    }
}

public sealed class ModelConfig
{
    public ModelType Type { get; set; } = ModelType.LargeV3Turbo;

    /// <summary>Where model files are cached; a relative path is anchored to the executable's directory.</summary>
    public string StorageDir { get; set; } = "./models";
}

public sealed class InferenceConfig
{
    /// <summary>Whisper language code ("zh", "en", "ja", ...) or "auto" to detect it.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>0 means one thread per CPU core.</summary>
    public int Threads { get; set; }

    public int BeamSize { get; set; } = 5;
}

public sealed class OutputConfig
{
    public OutputFormat Format { get; set; } = OutputFormat.Vtt;

    /// <summary>Print the run settings and every recognized segment.</summary>
    public bool Verbose { get; set; }
}
