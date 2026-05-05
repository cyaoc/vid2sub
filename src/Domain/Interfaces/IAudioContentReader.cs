namespace Vid2Sub.Domain.Interfaces;

public interface IAudioContentReader
{
    Task<Stream> OpenReadAsync(string audioPath, CancellationToken cancellationToken = default);
}
