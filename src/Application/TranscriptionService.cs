using System.Text;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;
using Vid2Sub.Domain.Services;

namespace Vid2Sub.Application;

/// <summary>The use case: one media file in, one subtitle file out.</summary>
public sealed class TranscriptionService(IAudioProcessor audioProcessor, ISpeechRecognizer recognizer)
{
    /// <summary>Subtitle path for an input: beside it, or in <paramref name="outputDir"/> when one is given.</summary>
    public static string OutputPathFor(string inputPath, string? outputDir, OutputFormat format)
    {
        var directory = string.IsNullOrEmpty(outputDir) ? Path.GetDirectoryName(inputPath) ?? "." : outputDir;
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(inputPath) + SubtitleFormatter.FileExtension(format));
    }

    /// <summary>Decodes, transcribes and writes the subtitle file. Returns the number of segments written.</summary>
    public async Task<int> TranscribeAsync(
        string inputPath,
        string outputPath,
        OutputFormat format,
        Action<TranscriptionSegment>? onSegment = null,
        CancellationToken cancellationToken = default)
    {
        var samples = await audioProcessor.DecodeAsync(inputPath, cancellationToken);
        var segments = await recognizer.RecognizeAsync(samples, onSegment, cancellationToken);

        // Written in one go after transcription succeeds, so a failure never leaves a partial subtitle file.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllTextAsync(outputPath, SubtitleFormatter.Format(segments, format), Encoding.UTF8, cancellationToken);

        return segments.Count;
    }
}
