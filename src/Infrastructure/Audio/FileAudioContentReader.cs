using Vid2Sub.Domain.Interfaces;

namespace Vid2Sub.Infrastructure.Audio;

public sealed class FileAudioContentReader : IAudioContentReader
{
    public Task<Stream> OpenReadAsync(string audioPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(File.OpenRead(audioPath));
    }
}
