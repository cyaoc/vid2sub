using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Subtitles;

public static class SubtitleValidator
{
    private static readonly TimeSpan LongDurationThreshold = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan LargeGapThreshold = TimeSpan.FromSeconds(10);

    public static SubtitleValidationResult Validate(IReadOnlyList<TranscriptionSegment> segments)
    {
        var errors = new List<SubtitleValidationIssue>();
        var warnings = new List<SubtitleValidationIssue>();

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var index = i + 1;

            if (segment.Start >= segment.End)
            {
                errors.Add(new SubtitleValidationIssue(
                    SubtitleValidationCodes.InvalidTimeRange,
                    index,
                    "Segment start must be before end."));
            }

            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                errors.Add(new SubtitleValidationIssue(
                    SubtitleValidationCodes.EmptyText,
                    index,
                    "Segment text must not be empty."));
            }

            if (i > 0)
            {
                var previous = segments[i - 1];
                if (segment.Start < previous.Start)
                {
                    errors.Add(new SubtitleValidationIssue(
                        SubtitleValidationCodes.NonMonotonicTimeline,
                        index,
                        "Segment start must be monotonic."));
                }

                if (segment.Start < previous.End)
                {
                    errors.Add(new SubtitleValidationIssue(
                        SubtitleValidationCodes.OverlappingSegment,
                        index,
                        "Segment overlaps the previous segment."));
                }

                if (segment.Start - previous.End > LargeGapThreshold)
                {
                    warnings.Add(new SubtitleValidationIssue(
                        SubtitleValidationCodes.LargeGap,
                        index,
                        "Large gap before segment."));
                }
            }

            if (segment.End - segment.Start > LongDurationThreshold)
            {
                warnings.Add(new SubtitleValidationIssue(
                    SubtitleValidationCodes.LongDuration,
                    index,
                    "Segment duration is unusually long."));
            }
        }

        return new SubtitleValidationResult(errors, warnings);
    }
}
