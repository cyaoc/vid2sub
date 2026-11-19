using Vid2Sub.Domain.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vid2Sub.Infrastructure.Configuration;

/// <summary>Loads <see cref="AppConfiguration"/> from config.yaml.</summary>
public static class YamlConfigLoader
{
    private const string FileName = "config.yaml";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties() // config files written for older versions still carry removed keys (ffmpeg_path, temp_dir)
        .Build();

    /// <summary>
    /// Reads <paramref name="explicitPath"/> when given; otherwise config.yaml from the working directory,
    /// then from beside the executable; otherwise the defaults.
    /// </summary>
    public static async Task<AppConfiguration> LoadAsync(string? explicitPath, CancellationToken cancellationToken = default)
    {
        if (explicitPath is not null && !File.Exists(explicitPath))
        {
            throw new FileNotFoundException($"Config file not found: {explicitPath}");
        }

        string?[] candidates = [explicitPath, Path.GetFullPath(FileName), Path.Combine(AppContext.BaseDirectory, FileName)];
        var path = candidates.FirstOrDefault(File.Exists);
        var config = path is null ? new AppConfiguration() : Parse(await File.ReadAllTextAsync(path, cancellationToken));

        // Anchored to the executable so a portable build finds the models folder shipped beside it.
        config.Model.StorageDir = Path.GetFullPath(config.Model.StorageDir, AppContext.BaseDirectory);
        if (!string.IsNullOrWhiteSpace(config.Correction.Glossary))
        {
            config.Correction.Glossary = Path.GetFullPath(config.Correction.Glossary, AppContext.BaseDirectory);
        }

        return config;
    }

    public static AppConfiguration Parse(string yaml)
    {
        try
        {
            var config = Deserializer.Deserialize<AppConfiguration?>(yaml) ?? new AppConfiguration();

            // A section left empty in YAML ("model:") deserializes to null.
            config.Model ??= new ModelConfig();
            config.Inference ??= new InferenceConfig();
            config.Output ??= new OutputConfig();
            config.Correction ??= new CorrectionConfig();
            return config;
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException($"Invalid config file: {ex.InnerException?.Message ?? ex.Message} ({ex.Start})", ex);
        }
    }
}
