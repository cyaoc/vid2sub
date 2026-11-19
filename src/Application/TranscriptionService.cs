using System.Text;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;
using Vid2Sub.Domain.Services;

namespace Vid2Sub.Application;

/// <summary>The use case: one media file, or one existing subtitle file, in; one subtitle file out.</summary>
/// <param name="inputs">Every file the batch will read, so that no result replaces one that is still to be read.</param>
public sealed class TranscriptionService(
    IAudioProcessor audioProcessor,
    ISpeechRecognizer recognizer,
    ISubtitleCorrector? corrector = null,
    IEnumerable<string>? inputs = null)
{
    private readonly HashSet<string> _inputs = new((inputs ?? []).Select(Canonical), StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _claimed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Subtitle path for an input: beside it, or in <paramref name="outputDir"/> when one is given.</summary>
    public static string OutputPathFor(string inputPath, string? outputDir, OutputFormat format)
    {
        var directory = string.IsNullOrEmpty(outputDir) ? Path.GetDirectoryName(inputPath) ?? "." : outputDir;
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(inputPath) + SubtitleFormatter.FileExtension(format));
    }

    /// <summary>The list of corrections written beside a corrected subtitle file, for the person who reviews it.</summary>
    public static string ChangesPathFor(string outputPath) => Path.ChangeExtension(outputPath, ".changes.txt");

    /// <summary>
    /// Decodes and transcribes the input, or reads it when it already is a subtitle file; proofreads when a
    /// corrector is given; writes the subtitle file. Returns the number of segments written. When only the
    /// correction fails, the subtitles are still written, uncorrected, and the failure is thrown afterwards.
    /// The list of corrections is written with the subtitles it describes and removed when they have none.
    /// </summary>
    public async Task<int> TranscribeAsync(
        string inputPath,
        string outputPath,
        OutputFormat format,
        Action<TranscriptionSegment>? onSegment = null,
        CancellationToken cancellationToken = default)
    {
        var changesPath = ChangesPathFor(outputPath);
        Claim(inputPath, [outputPath, changesPath]);

        var segments = SubtitleParser.IsSubtitleFile(inputPath)
            ? await ReadSubtitlesAsync(inputPath, cancellationToken)
            : await recognizer.RecognizeAsync(await audioProcessor.DecodeAsync(inputPath, cancellationToken), onSegment, cancellationToken);

        IReadOnlyList<SegmentChange>? changes = null;
        Exception? correctionFailure = null;
        if (corrector is not null)
        {
            try
            {
                (segments, changes) = await corrector.CorrectAsync(segments, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The transcription is the expensive part, so it is kept and the failure reported once it is saved.
                correctionFailure = ex;
            }
        }

        await WriteAsync(outputPath, SubtitleFormatter.Format(segments, format), cancellationToken);
        if (changes is not null)
        {
            await WriteAsync(changesPath, ChangeReport.Format(changes), cancellationToken);
        }
        else if (File.Exists(changesPath))
        {
            // The list from an earlier run would describe corrections these subtitles do not have: the correction
            // failed, or it is turned off now. Looked for first, because File.Delete throws on a name too long to
            // exist, which the list's name can be when the subtitles' name is not.
            File.Delete(changesPath);
        }

        return correctionFailure is null
            ? segments.Count
            : throw new InvalidOperationException($"correction failed ({correctionFailure.Message}); {outputPath} was written without correction", correctionFailure);
    }

    /// <summary>
    /// Reserves the files this input will write or remove, before anything is read or written. Refuses a file
    /// that is an input of the batch, this one or one still to be read, and a file already reserved for another
    /// input: subtitles and correction lists share one folder and may be given the same name. Also refuses
    /// outputs that are one file under two names, where the second write would replace the first.
    /// </summary>
    private void Claim(string inputPath, string[] outputs)
    {
        _inputs.Add(Canonical(inputPath));
        var paths = outputs.Select(Canonical).ToList();
        if (paths.FirstOrDefault(_inputs.Contains) is { } input)
        {
            throw new InvalidOperationException($"the result would overwrite the input {input}; choose another output folder or format");
        }

        if (paths.FirstOrDefault(_claimed.Contains) is { } taken)
        {
            throw new InvalidOperationException($"another input already writes {taken}");
        }

        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() < paths.Count)
        {
            throw new InvalidOperationException($"the subtitles and the list of corrections would both be written to {paths[0]}; choose another output folder or format");
        }

        _claimed.UnionWith(paths);
    }

    /// <summary>
    /// The full path with every symbolic link on the way followed, so that two spellings of one file compare
    /// equal: /tmp is a link to /private/tmp on macOS, and an output folder may be a link to the input's.
    /// </summary>
    // shortcut: a Windows 8.3 short name is not seen as the same file, compare file identities if that matters
    private static string Canonical(string path)
    {
        var full = Path.GetFullPath(path);
        var resolved = Path.GetPathRoot(full) ?? "";
        foreach (var part in full[resolved.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, part);

            // A part that does not exist yet, such as the output file or a new output folder, is no link.
            FileSystemInfo entry = Directory.Exists(resolved) ? new DirectoryInfo(resolved) : new FileInfo(resolved);
            if (entry.LinkTarget is not null && entry.ResolveLinkTarget(returnFinalTarget: true) is { } target)
            {
                // The target can sit behind links of its own.
                resolved = Canonical(target.FullName);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Writes beside the file and renames over it. A failure then never leaves a partial file, and a file that
    /// is a hard link to an input, which <see cref="Canonical"/> cannot see, is replaced by a new file instead
    /// of having the data it shares with the input rewritten.
    /// </summary>
    private static async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        // A symbolic link is followed first, so that the file it points at is replaced and the link stays.
        var target = Canonical(path);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);

        // A short name of its own: the target's name with a suffix may not fit when the target's name is near the limit.
        var partial = Path.Combine(directory, $"{Path.GetRandomFileName()}.tmp");
        try
        {
            if (File.Exists(target))
            {
                // Copied rather than created, so that the new file has the permissions of the one it replaces from the start.
                File.Copy(target, partial);
            }

            await File.WriteAllTextAsync(partial, content, Encoding.UTF8, cancellationToken);
            File.Move(partial, target, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    private static async Task<IReadOnlyList<TranscriptionSegment>> ReadSubtitlesAsync(string inputPath, CancellationToken cancellationToken)
    {
        var segments = SubtitleParser.Parse(await File.ReadAllTextAsync(inputPath, cancellationToken));
        return segments.Count > 0 ? segments : throw new InvalidDataException($"no subtitle cues found in {inputPath}");
    }
}
