using System.Text.Json;

namespace Vid2Sub.Infrastructure.Workflow;

public static class WorkflowArtifacts
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<ToolResult<string>> WriteJsonAsync<T>(
        string path,
        string outputRoot,
        bool overwriteConfirmed,
        T payload,
        CancellationToken cancellationToken = default)
    {
        var target = PathScopeGuard.ValidateWriteTarget(path, outputRoot, overwriteConfirmed);
        if (target.Status == ToolStatus.Failed)
        {
            return ToolResult<string>.Failure(target.Code!, target.Message!, target.Details);
        }

        var directory = Path.GetDirectoryName(target.Data!);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            target.Data!,
            JsonSerializer.Serialize(payload, JsonOptions),
            cancellationToken);

        return ToolResult<string>.Success(target.Data!);
    }
}

public sealed record WorkflowManifest(
    string InputPath,
    string WorkbookPath,
    string OutputRoot,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<ToolWarning> Warnings);

public sealed record GlossaryAudit(
    IReadOnlyList<string> MatchedKeys,
    IReadOnlyList<string> UnmatchedKeys,
    IReadOnlyList<ToolWarning> Warnings);
