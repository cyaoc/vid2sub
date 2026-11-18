namespace Vid2Sub.Infrastructure.Files;

/// <summary>Expands command-line inputs (files, directories, wildcards) into media file paths.</summary>
public static class InputFileCollector
{
    /// <summary>Extensions picked up when scanning a directory or wildcard; a file named explicitly is always tried.</summary>
    public static readonly IReadOnlySet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".mkv", ".avi",
        ".m4a", ".mp3", ".wav", ".aac"
    };

    /// <returns>The files to process, in input order without duplicates, and the inputs that matched nothing.</returns>
    public static (IReadOnlyList<string> Files, IReadOnlyList<string> Missing) Collect(IEnumerable<string> inputs)
    {
        var files = new List<string>();
        var missing = new List<string>();

        foreach (var input in inputs)
        {
            var fullPath = Path.GetFullPath(input);
            var directory = Path.GetDirectoryName(fullPath);

            string[] matches =
                File.Exists(fullPath) ? [fullPath]
                : Directory.Exists(fullPath) ? MediaFilesIn(fullPath, "*")
                : Directory.Exists(directory) ? MediaFilesIn(directory, Path.GetFileName(fullPath))
                : [];

            if (matches.Length == 0)
            {
                missing.Add(input);
            }

            files.AddRange(matches);
        }

        return ([.. files.Distinct()], missing);
    }

    private static string[] MediaFilesIn(string directory, string pattern) =>
        [.. Directory.EnumerateFiles(directory, pattern)
            .Where(file => MediaExtensions.Contains(Path.GetExtension(file)))
            .Order()];
}
