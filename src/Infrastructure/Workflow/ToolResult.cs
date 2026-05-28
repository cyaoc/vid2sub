namespace Vid2Sub.Infrastructure.Workflow;

public enum ToolStatus
{
    Success,
    Failed
}

public sealed record ToolResult<T>(
    ToolStatus Status,
    T? Data,
    string? Code,
    string? Message,
    string? Details,
    IReadOnlyList<ToolWarning> Warnings)
{
    public static ToolResult<T> Success(T data, IReadOnlyList<ToolWarning>? warnings = null) =>
        new(ToolStatus.Success, data, null, null, null, warnings ?? []);

    public static ToolResult<T> Failure(
        string code,
        string message,
        string? details = null,
        IReadOnlyList<ToolWarning>? warnings = null) =>
        new(ToolStatus.Failed, default, code, message, details, warnings ?? []);
}

public sealed record ToolWarning(
    string Code,
    string Message,
    string? Details = null);

public static class WorkflowErrorCodes
{
    public const string PathOutsideScope = "path_outside_scope";
    public const string OverwriteNotConfirmed = "overwrite_not_confirmed";
    public const string FileNotFound = "file_not_found";
    public const string InvalidWorkbook = "invalid_workbook";
    public const string MissingSheet = "missing_sheet";
    public const string MissingColumn = "missing_column";
    public const string UnsupportedFormat = "unsupported_format";
    public const string ProcessFailed = "process_failed";
}
