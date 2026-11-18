using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Interfaces;

/// <summary>Speech-to-text over 16 kHz mono samples. Owns the native model, so dispose it.</summary>
public interface ISpeechRecognizer : IAsyncDisposable
{
    /// <param name="samples">16 kHz mono audio.</param>
    /// <param name="onSegment">Called as each segment is recognized, before the full list is returned.</param>
    Task<IReadOnlyList<TranscriptionSegment>> RecognizeAsync(
        ReadOnlyMemory<float> samples,
        Action<TranscriptionSegment>? onSegment = null,
        CancellationToken cancellationToken = default);
}
