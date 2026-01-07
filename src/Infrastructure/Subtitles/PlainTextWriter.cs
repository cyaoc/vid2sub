using System.Text;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Subtitles;

/// <summary>
/// 纯文本格式写入器
/// 仅输出识别的文本内容，不包含时间戳
/// </summary>
public sealed class PlainTextWriter : ISubtitleWriter
{
    /// <inheritdoc />
    public string FileExtension => ".txt";
    
    /// <inheritdoc />
    public async Task WriteAsync(
        IEnumerable<TranscriptionSegment> segments,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var content = Format(segments);
        await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8, cancellationToken);
    }
    
    /// <inheritdoc />
    public string Format(IEnumerable<TranscriptionSegment> segments)
    {
        var sb = new StringBuilder();
        
        foreach (var segment in segments)
        {
            sb.AppendLine(segment.Text.Trim());
        }
        
        return sb.ToString();
    }
}
