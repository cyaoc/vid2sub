using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Interfaces;

public interface IWhisperProcessor : IAsyncDisposable
{
    IAsyncEnumerable<TranscriptionSegment> ProcessAsync(
        Stream audioStream,
        CancellationToken cancellationToken = default);
}
