using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Subtitles;

public sealed record SubtitleDocument(
    OutputFormat Format,
    IReadOnlyList<TranscriptionSegment> Segments);

public sealed record SubtitleValidationResult(
    IReadOnlyList<SubtitleValidationIssue> Errors,
    IReadOnlyList<SubtitleValidationIssue> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record SubtitleValidationIssue(
    string Code,
    int Index,
    string Message);

public static class SubtitleValidationCodes
{
    public const string InvalidTimeRange = "invalid_time_range";
    public const string NonMonotonicTimeline = "non_monotonic_timeline";
    public const string OverlappingSegment = "overlapping_segment";
    public const string EmptyText = "empty_text";
    public const string LongDuration = "long_duration";
    public const string LargeGap = "large_gap";
}
