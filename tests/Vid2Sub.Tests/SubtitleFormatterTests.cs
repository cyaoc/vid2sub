using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Domain.Services;

namespace Vid2Sub.Tests;

public class SubtitleFormatterTests
{
    private static readonly TranscriptionSegment[] Segments =
    [
        new(TimeSpan.Zero, TimeSpan.FromMilliseconds(2500), " Hello "),
        new(new TimeSpan(0, 1, 2, 3, 45), new TimeSpan(0, 26, 0, 0, 7), "World")
    ];

    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + Environment.NewLine));

    [Fact]
    public void Srt_numbers_cues_and_uses_comma_milliseconds() =>
        Assert.Equal(
            Lines("1", "00:00:00,000 --> 00:00:02,500", "Hello", "", "2", "01:02:03,045 --> 26:00:00,007", "World", ""),
            SubtitleFormatter.Format(Segments, OutputFormat.Srt));

    [Fact]
    public void Vtt_starts_with_header_and_uses_dot_milliseconds() =>
        Assert.Equal(
            Lines("WEBVTT", "", "1", "00:00:00.000 --> 00:00:02.500", "Hello", "", "2", "01:02:03.045 --> 26:00:00.007", "World", ""),
            SubtitleFormatter.Format(Segments, OutputFormat.Vtt));

    [Fact]
    public void Text_is_one_trimmed_line_per_segment() =>
        Assert.Equal(Lines("Hello", "World"), SubtitleFormatter.Format(Segments, OutputFormat.Text));

    [Theory]
    [InlineData(OutputFormat.Srt, ".srt")]
    [InlineData(OutputFormat.Vtt, ".vtt")]
    [InlineData(OutputFormat.Text, ".txt")]
    public void Each_format_has_its_file_extension(OutputFormat format, string extension) =>
        Assert.Equal(extension, SubtitleFormatter.FileExtension(format));
}
