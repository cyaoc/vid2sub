using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using YamlDotNet.Core;

namespace Vid2Sub.Infrastructure.Configuration;

public sealed class ConfigurationResolver(
    string workingDirectory,
    string appDirectory)
{
    private readonly YamlConfigLoader _loader = new();

    public async Task<ConfigurationResult> ResolveAsync(
        string? configPath,
        CliOptions cliOptions,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ConfigurationError>();
        var selectedConfigPath = configPath ?? YamlConfigLoader.GetDefaultConfigPath();
        var configSource = File.Exists(selectedConfigPath) ? selectedConfigPath : "defaults";
        var rawConfig = new AppConfiguration();

        if (!string.IsNullOrWhiteSpace(configPath) && !File.Exists(selectedConfigPath))
        {
            errors.Add(new ConfigurationError(configPath, "config", selectedConfigPath, "Selected config file does not exist."));
        }

        if (File.Exists(selectedConfigPath))
        {
            try
            {
                var yaml = await File.ReadAllTextAsync(selectedConfigPath, cancellationToken);
                if (ContainsPromptConfiguration(yaml))
                {
                    errors.Add(new ConfigurationError(configSource, "inference.prompt", null, "Prompt support has been removed."));
                }

                rawConfig = _loader.Load(yaml);
            }
            catch (YamlException ex)
            {
                errors.Add(new ConfigurationError(configSource, "yaml", null, $"Config parsing failed: {ex.Message}"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new ConfigurationError(configSource, "config", selectedConfigPath, $"Config file could not be read: {ex.Message}"));
            }
        }

        var modelText = Choose(cliOptions.Model, rawConfig.Model.Type);
        var formatText = Choose(cliOptions.Format, rawConfig.Output.Format);
        var language = Choose(cliOptions.Language, rawConfig.Inference.Language);
        var logLevelText = Choose(cliOptions.LogLevel, rawConfig.Output.LogLevel);
        var threads = cliOptions.Threads ?? rawConfig.Inference.Threads;
        var outputDir = string.IsNullOrWhiteSpace(cliOptions.OutputDir)
            ? EmptyToNull(rawConfig.Output.OutputDir)
            : cliOptions.OutputDir;

        if (!TryParseModelType(modelText, out var modelType))
        {
            errors.Add(new ConfigurationError(configSource, "model.type", modelText, "Model type must be Tiny, Base, Small, Medium, LargeV3, or LargeV3Turbo."));
        }

        if (!TryParseOutputFormat(formatText, out var outputFormat))
        {
            errors.Add(new ConfigurationError(configSource, "output.format", formatText, "Output format must be text, txt, srt, or vtt."));
        }

        if (!TryParseLogLevel(logLevelText, out var logLevel))
        {
            errors.Add(new ConfigurationError(configSource, "output.log_level", logLevelText, "Log level must be quiet, error, warning, information, or debug."));
        }

        if (threads < 0)
        {
            errors.Add(new ConfigurationError(configSource, "inference.threads", threads.ToString(), "Threads must be 0 or greater."));
        }

        if (rawConfig.Inference.BeamSize <= 0)
        {
            errors.Add(new ConfigurationError(configSource, "inference.beam_size", rawConfig.Inference.BeamSize.ToString(), "Beam size must be greater than 0."));
        }

        var modelStorageDir = ResolvePath(rawConfig.Model.StorageDir, appDirectory);
        var tempDir = ResolvePath(rawConfig.Environment.TempDir, appDirectory);
        var ffmpegPath = ResolveExecutablePath(rawConfig.Environment.FfmpegPath, appDirectory);
        var resolvedOutputDir = outputDir is null ? null : ResolvePath(outputDir, workingDirectory);

        ValidateConfiguredPath(errors, configSource, "model.storage_dir", modelStorageDir, requireExisting: false);
        ValidateConfiguredPath(errors, configSource, "environment.temp_dir", tempDir, requireExisting: false);
        if (resolvedOutputDir is not null)
        {
            ValidateConfiguredPath(errors, configSource, "output.output_dir", resolvedOutputDir, requireExisting: false);
        }

        if (Path.IsPathFullyQualified(ffmpegPath) && !File.Exists(ffmpegPath))
        {
            errors.Add(new ConfigurationError(configSource, "environment.ffmpeg_path", ffmpegPath, "FFmpeg executable path does not exist."));
        }

        if (errors.Count > 0)
        {
            return ConfigurationResult.Failure(errors);
        }

        var configuration = new ResolvedAppConfiguration(
            new ResolvedModelConfiguration(modelType, modelStorageDir),
            new ResolvedInferenceConfiguration(language, threads, rawConfig.Inference.BeamSize),
            new ResolvedEnvironmentConfiguration(ffmpegPath, tempDir),
            new ResolvedOutputConfiguration(outputFormat, logLevel, resolvedOutputDir, cliOptions.Overwrite));

        return ConfigurationResult.Success(configuration);
    }

    public static bool TryParseModelType(string value, out ModelType modelType)
    {
        modelType = value.Trim().ToLowerInvariant() switch
        {
            "tiny" => ModelType.Tiny,
            "base" => ModelType.Base,
            "small" => ModelType.Small,
            "medium" => ModelType.Medium,
            "largev3" or "large" => ModelType.LargeV3,
            "largev3turbo" or "turbo" => ModelType.LargeV3Turbo,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is "tiny" or "base" or "small" or "medium" or "largev3" or "large" or "largev3turbo" or "turbo";
    }

    public static bool TryParseOutputFormat(string value, out OutputFormat format)
    {
        format = value.Trim().ToLowerInvariant() switch
        {
            "text" or "txt" => OutputFormat.Text,
            "srt" => OutputFormat.Srt,
            "vtt" => OutputFormat.Vtt,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is "text" or "txt" or "srt" or "vtt";
    }

    public static bool TryParseLogLevel(string value, out Vid2SubLogLevel logLevel)
    {
        logLevel = value.Trim().ToLowerInvariant() switch
        {
            "quiet" => Vid2SubLogLevel.Quiet,
            "error" => Vid2SubLogLevel.Error,
            "warning" or "warn" => Vid2SubLogLevel.Warning,
            "information" or "info" => Vid2SubLogLevel.Information,
            "debug" => Vid2SubLogLevel.Debug,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is "quiet" or "error" or "warning" or "warn" or "information" or "info" or "debug";
    }

    private static string Choose(string? cliValue, string configValue) =>
        string.IsNullOrWhiteSpace(cliValue) ? configValue : cliValue;

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool ContainsPromptConfiguration(string yaml)
    {
        return yaml.Split('\n').Any(line =>
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith("prompt:", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("prompt_file:", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string ResolvePath(string path, string baseDirectory)
    {
        return Path.GetFullPath(Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(baseDirectory, path));
    }

    private static string ResolveExecutablePath(string path, string baseDirectory)
    {
        if (!path.Contains(Path.DirectorySeparatorChar) && !path.Contains(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return ResolvePath(path, baseDirectory);
    }

    private static void ValidateConfiguredPath(
        List<ConfigurationError> errors,
        string source,
        string key,
        string path,
        bool requireExisting)
    {
        if (requireExisting && !Directory.Exists(path))
        {
            errors.Add(new ConfigurationError(source, key, path, "Configured path does not exist."));
            return;
        }

        var target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(target))
        {
            errors.Add(new ConfigurationError(source, key, path, "Configured path is invalid."));
        }
    }
}
