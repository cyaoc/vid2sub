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
    public CorrectionConfig Correction { get; set; } = new();

    /// <summary>Applies the command-line values that were actually given, then validates the result.</summary>
    public void ApplyOverrides(
        OutputFormat? format = null,
        string? language = null,
        ModelType? model = null,
        int? threads = null,
        bool verbose = false,
        string? glossary = null,
        string? glossaryColumn = null)
    {
        Output.Format = format ?? Output.Format;
        Output.Verbose |= verbose;
        Correction.GlossaryColumn = string.IsNullOrWhiteSpace(glossaryColumn) ? Correction.GlossaryColumn : glossaryColumn;
        if (!string.IsNullOrWhiteSpace(glossary))
        {
            // Naming a glossary on the command line is the request to correct.
            Correction.Glossary = glossary;
            Correction.Enabled = true;
        }

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

/// <summary>Proofreading of the recognized text by the local Claude Code CLI.</summary>
public sealed class CorrectionConfig
{
    public bool Enabled { get; set; }

    /// <summary>Model ID passed to <c>claude --model</c>.</summary>
    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>
    /// Passed to <c>claude --effort</c>, so every machine proofreads the same way whatever its own Claude Code default is.
    /// Medium matched the higher levels on a real transcript in well under half the time; Low left overlong cues and missed terms.
    /// </summary>
    public CorrectionEffort Effort { get; set; } = CorrectionEffort.Medium;

    /// <summary>Term list, an .xlsx workbook or a text file; a relative path in YAML is anchored to the executable's directory.</summary>
    public string? Glossary { get; set; }

    /// <summary>
    /// Header of the workbook column that holds the terms in the language of the subtitles ("ja", "zh", ...).
    /// The other columns are translations, which a proofreader has no use for. Not needed for a single-column glossary.
    /// </summary>
    public string? GlossaryColumn { get; set; }
}
