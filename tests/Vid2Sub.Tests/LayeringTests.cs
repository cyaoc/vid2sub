namespace Vid2Sub.Tests;

/// <summary>Enforces the dependency rules in AGENTS.md so they cannot drift silently.</summary>
public class LayeringTests
{
    private static readonly string SourceRoot = Path.Combine(RepositoryRoot(), "src");

    [Theory]
    // Domain is pure: no other layer, no third-party library.
    [InlineData("Domain", "Vid2Sub.Application|Vid2Sub.Infrastructure|Vid2Sub.CLI|Whisper.net|ManagedCodecs|YamlDotNet|System.CommandLine")]
    // Application orchestrates through Domain interfaces only.
    [InlineData("Application", "Vid2Sub.Infrastructure|Vid2Sub.CLI|Whisper.net|ManagedCodecs|YamlDotNet|System.CommandLine")]
    // Infrastructure implements Domain interfaces and never reaches up.
    [InlineData("Infrastructure", "Vid2Sub.Application|Vid2Sub.CLI|System.CommandLine")]
    // Only the CLI talks to the console.
    [InlineData("Domain", "Console.")]
    [InlineData("Application", "Console.")]
    [InlineData("Infrastructure", "Console.")]
    // Hardware and OS selection belongs to Whisper.net's autoprobe, nowhere in our code.
    [InlineData("", "OperatingSystem.Is|RuntimeInformation.")]
    public void Layer_does_not_use(string layer, string forbidden)
    {
        var offences =
            from file in Directory.EnumerateFiles(Path.Combine(SourceRoot, layer), "*.cs", SearchOption.AllDirectories)
            where !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            from line in File.ReadLines(file)
            where !line.TrimStart().StartsWith("//")
            from token in forbidden.Split('|')
            where line.Contains(token)
            select $"{Path.GetRelativePath(SourceRoot, file)}: {line.Trim()}";

        Assert.Empty(offences);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "vid2sub.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("vid2sub.slnx not found above the test output directory");
    }
}
