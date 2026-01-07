using System.Text;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Subtitles;

/// <summary>
/// WebVTT 字幕格式写入器
/// WebVTT 是 HTML5 标准的字幕格式
/// </summary>
public sealed class VttWriter : ISubtitleWriter
{
    /// <inheritdoc />
    public string FileExtension => ".vtt";
    
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
        
        // WebVTT 文件头
        sb.AppendLine("WEBVTT");
        sb.AppendLine();
        
        var index = 1;
        
        foreach (var segment in segments)
        {
            // 可选的 cue 标识符
            sb.AppendLine(index.ToString());
            
            // 时间戳: 00:00:00.000 --> 00:00:00.000
            sb.AppendLine($"{FormatTimestamp(segment.Start)} --> {FormatTimestamp(segment.End)}");
            
            // 文本内容
            sb.AppendLine(segment.Text.Trim());
            
            // 空行分隔
            sb.AppendLine();
            
            index++;
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 格式化时间戳为 VTT 格式 (HH:MM:SS.mmm)
    /// </summary>
    private static string FormatTimestamp(TimeSpan time)
    {
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }
}
