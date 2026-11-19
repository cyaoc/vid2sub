using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Domain.Services;

namespace Vid2Sub.Tests;

public class SubtitleParserTests
{
    private static readonly TranscriptionSegment[] Segments =
    [
        new(TimeSpan.Zero, new TimeSpan(0, 0, 0, 1, 380), "ゲストをお招きしたいと思います"),
        new(new TimeSpan(0, 1, 2, 3, 4), new TimeSpan(0, 1, 2, 5, 60), "two\nlines"),
    ];

    [Theory]
    [InlineData(OutputFormat.Srt)]
    [InlineData(OutputFormat.Vtt)]
    public void What_the_formatter_writes_is_read_back_exactly(OutputFormat format) =>
        Assert.Equal(Segments, SubtitleParser.Parse(SubtitleFormatter.Format(Segments, format)));

    [Fact]
    public void WebVtt_extras_are_skipped_and_short_timestamps_are_understood()
    {
        var cues = SubtitleParser.Parse(
            "WEBVTT - made elsewhere\r\n\r\nNOTE a comment\r\nover two lines\r\n\r\nSTYLE\r\n::cue { color: red }\r\n\r\n"
            + "intro\r\n01:02.5 --> 01:04.250 align:start position:0%\r\nhello\r\n \r\n\r\n"
            + "00:00:09.000 --> 00:00:10.000\r\n\r\n");

        Assert.Equal([new TranscriptionSegment(new TimeSpan(0, 0, 1, 2, 500), new TimeSpan(0, 0, 1, 4, 250), "hello")], cues);
    }

    [Theory]
    [InlineData("clip.srt", true)]
    [InlineData("CLIP.VTT", true)]
    [InlineData("clip.txt", false)]
    [InlineData("clip.mp4", false)]
    public void Only_timed_subtitle_formats_count_as_subtitle_files(string path, bool expected) =>
        Assert.Equal(expected, SubtitleParser.IsSubtitleFile(path));
}
