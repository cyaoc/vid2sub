using System.Globalization;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Subtitles;

public sealed class SubtitleParser
{
    public async Task<SubtitleDocument> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Subtitle file not found", path);
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        var extension = Path.GetExtension(path);
        if (extension.Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            return new SubtitleDocument(OutputFormat.Srt, ParseSrt(content));
        }

        if (extension.Equals(".vtt", StringComparison.OrdinalIgnoreCase))
        {
            return new SubtitleDocument(OutputFormat.Vtt, ParseVtt(content));
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".text", StringComparison.OrdinalIgnoreCase))
        {
            var text = content.Trim();
            var segments = string.IsNullOrEmpty(text)
                ? []
                : new[] { new TranscriptionSegment(TimeSpan.Zero, TimeSpan.Zero, text) };
            return new SubtitleDocument(OutputFormat.Text, segments);
        }

        throw new InvalidDataException($"Unsupported subtitle format: {extension}");
    }

    private static IReadOnlyList<TranscriptionSegment> ParseSrt(string content) =>
        ParseBlocks(content, isVtt: false);

    private static IReadOnlyList<TranscriptionSegment> ParseVtt(string content)
    {
        var normalized = NormalizeNewLines(content);
        if (normalized.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
        {
            var firstBreak = normalized.IndexOf('\n');
            normalized = firstBreak >= 0 ? normalized[(firstBreak + 1)..] : string.Empty;
        }

        return ParseBlocks(normalized, isVtt: true);
    }

    private static IReadOnlyList<TranscriptionSegment> ParseBlocks(string content, bool isVtt)
    {
        var segments = new List<TranscriptionSegment>();
        var blocks = NormalizeNewLines(content)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var block in blocks)
        {
            var lines = block
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (lines.Count == 0)
            {
                continue;
            }

            var timeLineIndex = lines.FindIndex(line => line.Contains("-->", StringComparison.Ordinal));
            if (timeLineIndex < 0)
            {
                continue;
            }

            var times = lines[timeLineIndex].Split("-->", StringSplitOptions.TrimEntries);
            if (times.Length != 2)
            {
                throw new InvalidDataException($"Invalid subtitle timestamp line: {lines[timeLineIndex]}");
            }

            var text = string.Join(Environment.NewLine, lines.Skip(timeLineIndex + 1)).Trim();
            segments.Add(new TranscriptionSegment(
                ParseTimestamp(times[0], isVtt),
                ParseTimestamp(times[1], isVtt),
                text));
        }

        return segments;
    }

    private static TimeSpan ParseTimestamp(string value, bool isVtt)
    {
        var normalized = isVtt ? value.Trim() : value.Trim().Replace(',', '.');
        if (TimeSpan.TryParseExact(normalized, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out var time))
        {
            return time;
        }

        if (TimeSpan.TryParseExact(normalized, @"h\:mm\:ss\.fff", CultureInfo.InvariantCulture, out time))
        {
            return time;
        }

        throw new InvalidDataException($"Invalid subtitle timestamp: {value}");
    }

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n');
}
