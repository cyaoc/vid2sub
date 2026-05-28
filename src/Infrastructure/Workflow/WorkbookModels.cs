namespace Vid2Sub.Infrastructure.Workflow;

public sealed record WorkbookInspection(
    IReadOnlyList<WorkbookSheetInfo> Sheets);

public sealed record WorkbookSheetInfo(
    string Name,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> SampleRows);

public sealed record Glossary(
    IReadOnlyList<GlossaryEntry> Entries,
    IReadOnlyList<ToolWarning> Warnings);

public sealed record GlossaryEntry(
    string Key,
    string Translation,
    string? Notes);

public static class GlossaryWarningCodes
{
    public const string EmptyKey = "empty_key";
    public const string DuplicateKey = "duplicate_key";
}
