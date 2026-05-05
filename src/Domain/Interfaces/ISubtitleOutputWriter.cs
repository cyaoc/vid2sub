using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Interfaces;

public interface ISubtitleOutputWriter
{
    Task WriteAsync(
        string outputPath,
        IReadOnlyList<TranscriptionSegment> segments,
        CancellationToken cancellationToken = default);
}
