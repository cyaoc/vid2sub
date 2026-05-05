namespace Vid2Sub.Tests;

internal sealed class TestDirectory : IDisposable
{
    private TestDirectory(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public static TestDirectory Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "vid2sub-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TestDirectory(root);
    }

    public string CreateDirectory(string relativePath)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    public string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
