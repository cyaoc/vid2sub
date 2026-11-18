using System.Globalization;
using System.Text;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Services;

/// <summary>Renders segments as SRT, WebVTT or plain text.</summary>
public static class SubtitleFormatter
{
    public static string FileExtension(OutputFormat format) => format switch
    {
        OutputFormat.Srt => ".srt",
        OutputFormat.Vtt => ".vtt",
        OutputFormat.Text => ".txt",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };

    public static string Format(IEnumerable<TranscriptionSegment> segments, OutputFormat format)
    {
        var timed = format != OutputFormat.Text;
        var millisecondSeparator = format == OutputFormat.Srt ? ',' : '.';
        var builder = new StringBuilder();

        if (format == OutputFormat.Vtt)
        {
            builder.AppendLine("WEBVTT").AppendLine();
        }

        var index = 1;
        foreach (var segment in segments)
        {
            if (timed)
            {
                builder.AppendLine(index++.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine($"{Timestamp(segment.Start, millisecondSeparator)} --> {Timestamp(segment.End, millisecondSeparator)}");
            }

            builder.AppendLine(segment.Text.Trim());

            if (timed)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string Timestamp(TimeSpan time, char millisecondSeparator) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}{millisecondSeparator}{time.Milliseconds:000}";
}
