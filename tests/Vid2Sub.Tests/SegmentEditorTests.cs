using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Domain.Services;

namespace Vid2Sub.Tests;

public class SegmentEditorTests
{
    private static readonly TranscriptionSegment[] Segments =
    [
        Segment(0, 2, "one"),
        Segment(2, 4, "two"),
        Segment(6, 14, "three"),
        Segment(14, 15, "four"),
    ];

    [Fact]
    public void Merged_segments_span_from_the_first_start_to_the_last_end()
    {
        var (result, changes) = SegmentEditor.Apply(Segments, [new SegmentEdit(2, 3, ["two three"], ChangeKind.Term)]);

        Assert.Equal([Segments[0], Segment(2, 14, "two three"), Segments[3]], result);
        var change = Assert.Single(changes);
        Assert.Equal((TimeSpan.FromSeconds(2), ChangeKind.Term), (change.Start, change.Kind));
        Assert.Equal(["two", "three"], change.Before);
        Assert.Equal(["two three"], change.After);
    }

    [Fact]
    public void Merged_segments_that_overlap_span_from_the_earliest_start_to_the_latest_end()
    {
        // Subtitles read from a file may nest: the second cue here ends long before the first one does.
        TranscriptionSegment[] nested = [Segment(0, 10, "long"), Segment(2, 3, "short"), Segment(1, 4, "middle")];

        var (result, changes) = SegmentEditor.Apply(nested, [new SegmentEdit(1, 3, ["all three"])]);

        Assert.Equal([Segment(0, 10, "all three")], result);
        Assert.Equal(TimeSpan.Zero, Assert.Single(changes).Start);
    }

    [Fact]
    public void Split_segments_share_the_time_span_in_proportion_to_their_length()
    {
        var (result, _) = SegmentEditor.Apply(Segments, [new SegmentEdit(3, 3, ["ab", " cdefgh "])]);

        Assert.Equal([Segments[0], Segments[1], Segment(6, 8, "ab"), Segment(8, 14, "cdefgh"), Segments[3]], result);
    }

    [Fact]
    public void An_edit_without_lines_deletes_its_segments()
    {
        var (result, _) = SegmentEditor.Apply(Segments, [new SegmentEdit(1, 2, []), new SegmentEdit(4, 4, [" "])]);

        Assert.Equal([Segments[2]], result);
    }

    [Fact]
    public void Segments_without_an_edit_are_returned_untouched()
    {
        var (result, changes) = SegmentEditor.Apply(Segments, []);

        Assert.Equal(Segments, result);
        Assert.Empty(changes);
    }

    [Theory]
    [InlineData(0, 1)] // before the first segment
    [InlineData(4, 5)] // past the last segment
    [InlineData(3, 2)] // reversed
    [InlineData(1, 1)] // overlaps the edit before it
    public void An_invalid_edit_is_skipped_and_the_others_still_apply(int from, int to)
    {
        var (result, changes) = SegmentEditor.Apply(Segments, [new SegmentEdit(1, 1, ["ONE"]), new SegmentEdit(from, to, ["broken"])]);

        Assert.Equal([Segment(0, 2, "ONE"), Segments[1], Segments[2], Segments[3]], result);
        Assert.Equal(["ONE"], Assert.Single(changes).After);
    }

    private static TranscriptionSegment Segment(double start, double end, string text) =>
        new(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), text);
}
