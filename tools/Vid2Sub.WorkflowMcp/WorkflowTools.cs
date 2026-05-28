using System.ComponentModel;
using ModelContextProtocol.Server;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Subtitles;
using Vid2Sub.Infrastructure.Workflow;

namespace Vid2Sub.WorkflowMcp;

[McpServerToolType]
public sealed class WorkflowTools(
    WorkbookInspector workbookInspector,
    GlossaryReader glossaryReader,
    Vid2SubProcessRunner vid2SubProcessRunner)
{
    [McpServerTool(Name = "run_vid2sub", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Run the compiled vid2sub executable to generate raw subtitles.")]
    public Task<ToolResult<RunVid2SubResult>> RunVid2Sub(
        string executablePath,
        string inputPath,
        string outputRoot,
        string outputPath,
        string format,
        string? language,
        string? model,
        bool overwriteConfirmed,
        CancellationToken cancellationToken = default) =>
        vid2SubProcessRunner.RunAsync(
            new RunVid2SubRequest(
                executablePath,
                inputPath,
                outputRoot,
                outputPath,
                format,
                language,
                model,
                overwriteConfirmed),
            cancellationToken);

    [McpServerTool(Name = "inspect_workbook", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect workbook sheets, columns, and bounded sample rows.")]
    public async Task<ToolResult<WorkbookInspection>> InspectWorkbook(
        string path,
        int sampleRowCount = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return ToolResult<WorkbookInspection>.Success(
                await workbookInspector.InspectAsync(path, sampleRowCount, cancellationToken));
        }
        catch (FileNotFoundException ex)
        {
            return ToolResult<WorkbookInspection>.Failure(WorkflowErrorCodes.FileNotFound, ex.Message, ex.FileName);
        }
        catch (Exception ex)
        {
            return ToolResult<WorkbookInspection>.Failure(WorkflowErrorCodes.InvalidWorkbook, "Workbook could not be inspected.", ex.Message);
        }
    }

    [McpServerTool(Name = "read_glossary", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read glossary entries from a confirmed workbook sheet and column mapping.")]
    public async Task<ToolResult<Glossary>> ReadGlossary(
        string path,
        string sheet,
        string keyColumn,
        string translationColumn,
        string? notesColumn,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return ToolResult<Glossary>.Success(
                await glossaryReader.ReadAsync(path, sheet, keyColumn, translationColumn, notesColumn, cancellationToken));
        }
        catch (FileNotFoundException ex)
        {
            return ToolResult<Glossary>.Failure(WorkflowErrorCodes.FileNotFound, ex.Message, ex.FileName);
        }
        catch (InvalidDataException ex)
        {
            return ToolResult<Glossary>.Failure(WorkflowErrorCodes.InvalidWorkbook, ex.Message);
        }
    }

    [McpServerTool(Name = "parse_subtitles", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Parse an SRT, VTT, or text subtitle file into structured segments.")]
    public async Task<ToolResult<SubtitleDocument>> ParseSubtitles(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return ToolResult<SubtitleDocument>.Success(
                await new SubtitleParser().ParseFileAsync(path, cancellationToken));
        }
        catch (FileNotFoundException ex)
        {
            return ToolResult<SubtitleDocument>.Failure(WorkflowErrorCodes.FileNotFound, ex.Message, ex.FileName);
        }
        catch (Exception ex)
        {
            return ToolResult<SubtitleDocument>.Failure(WorkflowErrorCodes.UnsupportedFormat, "Subtitle file could not be parsed.", ex.Message);
        }
    }

    [McpServerTool(Name = "validate_segments", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Validate subtitle timing and text rules.")]
    public ToolResult<SubtitleValidationResult> ValidateSegments(IReadOnlyList<TranscriptionSegment> segments)
    {
        var result = SubtitleValidator.Validate(segments);
        return ToolResult<SubtitleValidationResult>.Success(result);
    }

    [McpServerTool(Name = "write_subtitles", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Validate and write subtitles inside an output root.")]
    public async Task<ToolResult<string>> WriteSubtitles(
        string path,
        string outputRoot,
        string format,
        IReadOnlyList<TranscriptionSegment> segments,
        bool overwriteConfirmed,
        CancellationToken cancellationToken = default)
    {
        var target = PathScopeGuard.ValidateWriteTarget(path, outputRoot, overwriteConfirmed);
        if (target.Status == ToolStatus.Failed)
        {
            return ToolResult<string>.Failure(target.Code!, target.Message!, target.Details);
        }

        var validation = SubtitleValidator.Validate(segments);
        if (!validation.IsValid)
        {
            return ToolResult<string>.Failure(
                "invalid_segments",
                "Subtitle segments failed validation.",
                string.Join("; ", validation.Errors.Select(error => $"{error.Index}:{error.Code}")));
        }

        if (!Enum.TryParse<OutputFormat>(format, ignoreCase: true, out var outputFormat))
        {
            return ToolResult<string>.Failure(
                WorkflowErrorCodes.UnsupportedFormat,
                $"Unsupported subtitle format: {format}");
        }

        await new SubtitleOutputWriter(outputFormat).WriteAsync(target.Data!, segments, cancellationToken);
        return ToolResult<string>.Success(target.Data!, validation.Warnings.Select(w => new ToolWarning(w.Code, w.Message, w.Index.ToString())).ToList());
    }
}
