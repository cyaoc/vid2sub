using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Whisper.net.Ggml;

namespace Vid2Sub.Infrastructure.Speech;

/// <summary>Caches ggml model files in a directory and downloads the missing ones.</summary>
/// <param name="storageDir">Directory holding the model files.</param>
/// <param name="downloadProgress">Receives 0..1 while a model downloads.</param>
/// <param name="download">Opens the model's download stream; defaults to Whisper.net's Hugging Face downloader.</param>
public sealed class WhisperModelProvider(
    string storageDir,
    IProgress<double>? downloadProgress = null,
    Func<GgmlType, CancellationToken, Task<Stream>>? download = null) : IModelProvider
{
    private readonly Func<GgmlType, CancellationToken, Task<Stream>> _download = download
        ?? ((type, cancellationToken) => WhisperGgmlDownloader.Default.GetGgmlModelAsync(type, cancellationToken: cancellationToken));

    public async Task<string> EnsureModelAsync(ModelType modelType, CancellationToken cancellationToken = default)
    {
        var (ggmlType, fileName, approximateBytes) = Describe(modelType);
        var modelPath = Path.Combine(storageDir, fileName);
        if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 0)
        {
            return modelPath;
        }

        Directory.CreateDirectory(storageDir);
        var partialPath = modelPath + ".download";
        try
        {
            await using (var source = await _download(ggmlType, cancellationToken))
            await using (var target = File.Create(partialPath))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    total += read;
                    downloadProgress?.Report(Math.Min((double)total / approximateBytes, 0.99));
                }

                if (total == 0)
                {
                    throw new InvalidDataException("the download was empty");
                }
            }

            // Renamed only when complete, so an interrupted download is never mistaken for a model.
            File.Move(partialPath, modelPath, overwrite: true);
            downloadProgress?.Report(1);
            return modelPath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Could not download the {modelType} model: {ex.Message}", ex);
        }
        finally
        {
            File.Delete(partialPath);
        }
    }

    // shortcut: sizes are approximate because the download stream reports no length, use Content-Length if exact progress matters
    private static (GgmlType Type, string FileName, long ApproximateBytes) Describe(ModelType modelType) => modelType switch
    {
        ModelType.Tiny => (GgmlType.Tiny, "ggml-tiny.bin", 75_000_000),
        ModelType.Base => (GgmlType.Base, "ggml-base.bin", 142_000_000),
        ModelType.Small => (GgmlType.Small, "ggml-small.bin", 466_000_000),
        ModelType.Medium => (GgmlType.Medium, "ggml-medium.bin", 1_500_000_000),
        ModelType.LargeV3 => (GgmlType.LargeV3, "ggml-large-v3.bin", 3_000_000_000),
        ModelType.LargeV3Turbo => (GgmlType.LargeV3Turbo, "ggml-large-v3-turbo.bin", 1_600_000_000),
        _ => throw new ArgumentOutOfRangeException(nameof(modelType), modelType, null)
    };
}
