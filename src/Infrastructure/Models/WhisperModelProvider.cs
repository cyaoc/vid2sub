using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;
using Whisper.net.Ggml;

namespace Vid2Sub.Infrastructure.Models;

/// <summary>
/// Provides Whisper model download, caching, and path lookup.
/// </summary>
public sealed class WhisperModelProvider : IModelProvider
{
    private readonly ResolvedModelConfiguration _config;
    private readonly IWhisperModelDownloader _downloader;

    public WhisperModelProvider(ResolvedModelConfiguration config)
        : this(config, new WhisperModelDownloader())
    {
    }

    public WhisperModelProvider(ResolvedModelConfiguration config, IWhisperModelDownloader downloader)
    {
        _config = config;
        _downloader = downloader;
    }

    public async Task<string> EnsureModelAsync(
        ModelType modelType,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var modelPath = GetModelPath(modelType);

        if (ModelExists(modelType))
        {
            progress?.Report(1.0);
            return modelPath;
        }

        var directory = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{modelPath}.{Guid.NewGuid():N}.tmp";
        var ggmlType = ToGgmlType(modelType);

        try
        {
            await using var modelStream = await _downloader.GetGgmlModelAsync(ggmlType, cancellationToken);
            await using (var fileStream = File.Create(tempPath))
            {
                if (progress is not null)
                {
                    await CopyWithProgressAsync(modelStream, fileStream, GetEstimatedModelSize(modelType), progress, cancellationToken);
                }
                else
                {
                    await modelStream.CopyToAsync(fileStream, cancellationToken);
                }
            }

            ValidateDownloadedModel(tempPath, modelType);
            File.Move(tempPath, modelPath, overwrite: true);
            progress?.Report(1.0);

            return modelPath;
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            throw new InvalidOperationException($"Model download failed for {modelType}: {ex.Message}", ex);
        }
    }

    public bool ModelExists(ModelType modelType)
    {
        var path = GetModelPath(modelType);
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    public string GetModelPath(ModelType modelType)
    {
        var storageDir = Path.GetFullPath(_config.StorageDir);
        return Path.Combine(storageDir, GetModelFileName(modelType));
    }

    public static long GetEstimatedModelSize(ModelType modelType) => modelType switch
    {
        ModelType.Tiny => 75_000_000,
        ModelType.Base => 142_000_000,
        ModelType.Small => 466_000_000,
        ModelType.Medium => 1_500_000_000,
        ModelType.LargeV3 => 3_000_000_000,
        ModelType.LargeV3Turbo => 1_600_000_000,
        _ => 1_500_000_000
    };

    private static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        long estimatedSize,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;
            progress.Report(Math.Min((double)totalRead / estimatedSize, 0.99));
        }
    }

    private static void ValidateDownloadedModel(string tempPath, ModelType modelType)
    {
        if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
        {
            throw new InvalidOperationException($"Downloaded {modelType} model is empty.");
        }
    }

    private static string GetModelFileName(ModelType modelType) => modelType switch
    {
        ModelType.Tiny => "ggml-tiny.bin",
        ModelType.Base => "ggml-base.bin",
        ModelType.Small => "ggml-small.bin",
        ModelType.Medium => "ggml-medium.bin",
        ModelType.LargeV3 => "ggml-large-v3.bin",
        ModelType.LargeV3Turbo => "ggml-large-v3-turbo.bin",
        _ => "ggml-medium.bin"
    };

    private static GgmlType ToGgmlType(ModelType modelType) => modelType switch
    {
        ModelType.Tiny => GgmlType.Tiny,
        ModelType.Base => GgmlType.Base,
        ModelType.Small => GgmlType.Small,
        ModelType.Medium => GgmlType.Medium,
        ModelType.LargeV3 => GgmlType.LargeV3,
        ModelType.LargeV3Turbo => GgmlType.LargeV3Turbo,
        _ => GgmlType.Medium
    };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; the original download error is more useful.
        }
    }

    private sealed class WhisperModelDownloader : IWhisperModelDownloader
    {
        public Task<Stream> GetGgmlModelAsync(
            GgmlType modelType,
            CancellationToken cancellationToken = default)
        {
            return WhisperGgmlDownloader.Default.GetGgmlModelAsync(
                modelType,
                cancellationToken: cancellationToken);
        }
    }
}
