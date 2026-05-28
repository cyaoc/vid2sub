using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Subtitles;

namespace Vid2Sub.Tests.Infrastructure;

public sealed class SubtitleCodecTests
{
    [Fact]
    public async Task SubtitleParser_ReadsSrtSegments()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("sample.srt", """
            1
            00:00:01,000 --> 00:00:03,500
            こんにちは

            2
            00:00:04,000 --> 00:00:05,000
            世界

            """);

        var document = await new SubtitleParser().ParseFileAsync(path);

        Assert.Equal(OutputFormat.Srt, document.Format);
        Assert.Equal(2, document.Segments.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), document.Segments[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(3500), document.Segments[0].End);
        Assert.Equal("こんにちは", document.Segments[0].Text);
    }

    [Fact]
    public async Task SubtitleParser_ReadsVttSegments()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("sample.vtt", """
            WEBVTT

            1
            00:00:01.000 --> 00:00:03.500
            hello

            """);

        var document = await new SubtitleParser().ParseFileAsync(path);

        Assert.Equal(OutputFormat.Vtt, document.Format);
        Assert.Single(document.Segments);
        Assert.Equal("hello", document.Segments[0].Text);
    }

    [Fact]
    public void SubtitleValidator_RejectsInvalidTimelineAndEmptyText()
    {
        var segments = new[]
        {
            new TranscriptionSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), "bad"),
            new TranscriptionSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "")
        };

        var result = SubtitleValidator.Validate(segments);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == SubtitleValidationCodes.InvalidTimeRange);
        Assert.Contains(result.Errors, e => e.Code == SubtitleValidationCodes.NonMonotonicTimeline);
        Assert.Contains(result.Errors, e => e.Code == SubtitleValidationCodes.EmptyText);
    }
}
