namespace Vid2Sub.Infrastructure.Workflow;

public static class PathScopeGuard
{
    public static ToolResult<string> ValidateWriteTarget(
        string path,
        string outputRoot,
        bool overwriteConfirmed)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(outputRoot);

        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            fullRoot += Path.DirectorySeparatorChar;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(fullRoot, comparison))
        {
            return ToolResult<string>.Failure(
                WorkflowErrorCodes.PathOutsideScope,
                "Path is outside the configured output root.",
                fullPath);
        }

        if (File.Exists(fullPath) && !overwriteConfirmed)
        {
            return ToolResult<string>.Failure(
                WorkflowErrorCodes.OverwriteNotConfirmed,
                "Output already exists and overwrite was not confirmed.",
                fullPath);
        }

        return ToolResult<string>.Success(fullPath);
    }
}
