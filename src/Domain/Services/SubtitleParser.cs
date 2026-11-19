using System.Text.RegularExpressions;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Services;

/// <summary>Reads SRT and WebVTT subtitles back into segments, so existing subtitles can be proofread again without transcribing.</summary>
public static partial class SubtitleParser
{
    public static bool IsSubtitleFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".srt" or ".vtt";

    /// <summary>
    /// Returns the cues in file order. Everything that is not a cue is skipped: the WEBVTT header, NOTE and STYLE
    /// blocks, cue numbers, and the cue settings WebVTT allows after the end time.
    /// </summary>
    public static IReadOnlyList<TranscriptionSegment> Parse(string content)
    {
        var segments = new List<TranscriptionSegment>();
        foreach (var block in BlankLines().Split(content.ReplaceLineEndings("\n")))
        {
            var lines = block.Split('\n');
            var timing = Array.FindIndex(lines, line => line.Contains("-->"));
            var match = timing < 0 ? Match.Empty : Timing().Match(lines[timing]);
            var text = string.Join('\n', lines.Skip(timing + 1)).Trim();
            if (match.Success && text.Length > 0)
            {
                segments.Add(new TranscriptionSegment(Time(match, "s"), Time(match, "e"), text));
            }
        }

        return segments;
    }

    // Built from whole numbers so that a timestamp survives a round trip exactly; a floating-point second would not.
    private static TimeSpan Time(Match match, string prefix) => new(
        days: 0,
        hours: match.Groups[prefix + "h"].Success ? int.Parse(match.Groups[prefix + "h"].Value) : 0,
        minutes: int.Parse(match.Groups[prefix + "m"].Value),
        seconds: int.Parse(match.Groups[prefix + "s"].Value),
        milliseconds: int.Parse(match.Groups[prefix + "f"].Value.PadRight(3, '0')));

    [GeneratedRegex(@"\n[ \t]*\n")]
    private static partial Regex BlankLines();

    // SRT writes 00:01:02,345 and WebVTT 00:01:02.345, where the hours may be left out.
    [GeneratedRegex(@"^\s*(?:(?<sh>\d+):)?(?<sm>\d{1,2}):(?<ss>\d{1,2})[.,](?<sf>\d{1,3})\s+-->\s+(?:(?<eh>\d+):)?(?<em>\d{1,2}):(?<es>\d{1,2})[.,](?<ef>\d{1,3})")]
    private static partial Regex Timing();
}
