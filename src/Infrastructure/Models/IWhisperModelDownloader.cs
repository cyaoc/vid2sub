using Whisper.net.Ggml;

namespace Vid2Sub.Infrastructure.Models;

/// <summary>
/// Thin abstraction over model download so provider behavior can be tested without network access.
/// </summary>
public interface IWhisperModelDownloader
{
    Task<Stream> GetGgmlModelAsync(
        GgmlType modelType,
        CancellationToken cancellationToken = default);
}
