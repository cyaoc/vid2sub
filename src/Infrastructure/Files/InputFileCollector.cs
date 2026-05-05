using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Files;

public sealed class InputFileCollector
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma"
    };

    public InputFileCollection Collect(IEnumerable<string> inputs)
    {
        var files = new List<InputFile>();
        var outcomes = new List<TranscriptionResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            var matchedFiles = ResolveInput(input);

            if (matchedFiles.Count == 0)
            {
                outcomes.Add(Failed(input, TranscriptionErrorCodes.InputMissing, $"Input not found: {input}"));
                continue;
            }

            foreach (var file in matchedFiles)
            {
                if (!seen.Add(file))
                {
                    outcomes.Add(new TranscriptionResult(
                        file,
                        [],
                        TranscriptionStatus.Skipped,
                        new TranscriptionError(TranscriptionStage.InputPlanning, TranscriptionErrorCodes.InputDuplicate, $"Duplicate input skipped: {file}", file)));
                    continue;
                }

                files.Add(new InputFile(input, file));
            }
        }

        return new InputFileCollection(files, outcomes);
    }

    private static List<string> ResolveInput(string input)
    {
        var fullPath = Path.GetFullPath(input);
        if (Directory.Exists(fullPath))
        {
            return Directory.EnumerateFiles(fullPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file)))
                .OrderBy(file => file)
                .ToList();
        }

        if (File.Exists(fullPath) && SupportedExtensions.Contains(Path.GetExtension(fullPath)))
        {
            return [fullPath];
        }

        var directory = Path.GetDirectoryName(fullPath);
        var pattern = Path.GetFileName(fullPath);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory) && HasWildcard(pattern))
        {
            return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file)))
                .OrderBy(file => file)
                .ToList();
        }

        return [];
    }

    private static bool HasWildcard(string value) => value.Contains('*') || value.Contains('?');

    private static TranscriptionResult Failed(string sourceFile, string code, string message) =>
        new(sourceFile, [], TranscriptionStatus.Failed, new TranscriptionError(TranscriptionStage.InputPlanning, code, message, sourceFile));
}

public sealed record InputFile(
    string OriginalInput,
    string FullPath);

public sealed record InputFileCollection(
    IReadOnlyList<InputFile> Files,
    IReadOnlyList<TranscriptionResult> Outcomes);
