namespace Vid2Sub.Infrastructure.Correction;

/// <summary>
/// The proofreading rules the corrector sends to the model: one file for every language, and one per language
/// for what only that language needs, such as where a Japanese cue may break. They are prose the user may
/// rewrite, so they live in files beside the executable instead of in code; the copies built into the program
/// are used for the files that are gone.
/// </summary>
public static class CorrectionRules
{
    public const string FileName = "correction-rules.md";

    /// <summary>
    /// Returns the common rules followed by those of <paramref name="language"/> (a Whisper code such as "ja"),
    /// and a description of where they came from. A language that is unknown or has no rules gets the common ones.
    /// </summary>
    public static (string Text, string Source) Load(string directory, string? language)
    {
        var (common, commonSource) = Part(directory, FileName)
            ?? throw new InvalidOperationException($"{FileName} is not embedded in the program");

        // The code becomes part of a file name, so anything that is not a plain language code counts as unknown.
        var code = language?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code) || !code.All(char.IsAsciiLetterLower))
        {
            return (common, $"{commonSource} (language not known, so no language rules)");
        }

        return Part(directory, $"correction-rules.{code}.md") is var (specific, specificSource)
            ? ($"{common.TrimEnd()}\n\n{specific}", $"{commonSource} + {specificSource}")
            : (common, $"{commonSource} (no rules for language \"{code}\")");
    }

    private static (string Text, string Source)? Part(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        if (File.Exists(path))
        {
            return (File.ReadAllText(path), name);
        }

        using var resource = typeof(CorrectionRules).Assembly.GetManifestResourceStream(name);
        if (resource is null)
        {
            return null;
        }

        using var reader = new StreamReader(resource);
        return (reader.ReadToEnd(), $"built-in {name}");
    }
}
