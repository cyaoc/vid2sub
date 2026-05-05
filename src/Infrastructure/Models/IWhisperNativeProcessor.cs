using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Models;

public interface IWhisperNativeProcessor : IAsyncDisposable
{
    IAsyncEnumerable<TranscriptionSegment> ProcessAsync(
        Stream audioStream,
        CancellationToken cancellationToken = default);
}
