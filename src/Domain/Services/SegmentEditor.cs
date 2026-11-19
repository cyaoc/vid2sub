using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Services;

/// <summary>Applies <see cref="SegmentEdit"/>s to segments and works out the timing of the new ones.</summary>
public static class SegmentEditor
{
    /// <summary>
    /// Returns the edited segments and a record of each edit that was applied. An edit that is out of range,
    /// reversed, or not after the previous one is skipped, which leaves the original segments in place there.
    /// Segments no edit names are returned untouched.
    /// </summary>
    public static (IReadOnlyList<TranscriptionSegment> Segments, IReadOnlyList<SegmentChange> Changes) Apply(
        IReadOnlyList<TranscriptionSegment> segments,
        IEnumerable<SegmentEdit> edits)
    {
        var result = new List<TranscriptionSegment>(segments.Count);
        var changes = new List<SegmentChange>();
        var next = 1; // number of the first segment not yet copied or replaced

        foreach (var edit in edits)
        {
            if (edit.From < next || edit.To < edit.From || edit.To > segments.Count)
            {
                continue;
            }

            for (; next < edit.From; next++)
            {
                result.Add(segments[next - 1]);
            }

            // The new segments share the replaced time span in proportion to their length: exact for a merge,
            // an estimate for a split.
            // shortcut: character counts stand in for speech timing, use Whisper token timestamps if split cues drift
            // Cues read from a subtitle file may overlap or nest, so the span is not simply first start to last end.
            var lines = edit.Lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            var replaced = segments.Skip(edit.From - 1).Take(edit.To - edit.From + 1).ToList();
            var start = replaced.Min(segment => segment.Start);
            var span = replaced.Max(segment => segment.End) - start;
            var total = lines.Sum(line => line.Length);
            var done = 0;
            foreach (var line in lines)
            {
                var lineStart = start + span * ((double)done / total);
                done += line.Length;
                result.Add(new TranscriptionSegment(lineStart, start + span * ((double)done / total), line));
            }

            changes.Add(new SegmentChange(start, edit.Kind, [.. replaced.Select(segment => segment.Text.Trim())], lines));
            next = edit.To + 1;
        }

        for (; next <= segments.Count; next++)
        {
            result.Add(segments[next - 1]);
        }

        return (result, changes);
    }
}
