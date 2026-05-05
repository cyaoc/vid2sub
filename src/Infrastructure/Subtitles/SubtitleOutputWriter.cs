using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Subtitles;

public sealed class SubtitleOutputWriter(OutputFormat format) : ISubtitleOutputWriter
{
    private readonly ISubtitleWriter _writer = SubtitleWriterFactory.Create(format);

    public Task WriteAsync(
        string outputPath,
        IReadOnlyList<TranscriptionSegment> segments,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return _writer.WriteAsync(segments, outputPath, cancellationToken);
    }
}
