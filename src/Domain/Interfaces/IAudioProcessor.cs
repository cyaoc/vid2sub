namespace Vid2Sub.Domain.Interfaces;

/// <summary>Turns a media file into the audio Whisper consumes.</summary>
public interface IAudioProcessor
{
    /// <summary>Decodes the file's audio to 16 kHz mono samples in [-1, 1].</summary>
    /// <exception cref="NotSupportedException">The container or audio codec is not supported.</exception>
    Task<ReadOnlyMemory<float>> DecodeAsync(string inputPath, CancellationToken cancellationToken = default);
}
