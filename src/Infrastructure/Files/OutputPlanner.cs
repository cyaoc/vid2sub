using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Subtitles;

namespace Vid2Sub.Infrastructure.Files;

public sealed class OutputPlanner
{
    public OutputPlan CreatePlan(InputFileCollection inputFiles, ResolvedAppConfiguration configuration)
    {
        var workItems = new List<TranscriptionWorkItem>();
        var outcomes = new List<TranscriptionResult>(inputFiles.Outcomes);
        var plannedOutputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var writer = SubtitleWriterFactory.Create(configuration.Output.Format);

        foreach (var input in inputFiles.Files)
        {
            var outputPath = DetermineOutputPath(input.FullPath, configuration.Output.OutputDir, writer.FileExtension);

            if (plannedOutputs.TryGetValue(outputPath, out var previousSource))
            {
                outcomes.Add(Failed(
                    input.FullPath,
                    TranscriptionErrorCodes.OutputCollision,
                    $"Output path collision: {previousSource} and {input.FullPath} both map to {outputPath}",
                    outputPath));
                continue;
            }

            plannedOutputs.Add(outputPath, input.FullPath);

            if (!configuration.Output.Overwrite && File.Exists(outputPath))
            {
                outcomes.Add(Failed(
                    input.FullPath,
                    TranscriptionErrorCodes.OutputExists,
                    $"Output already exists: {outputPath}",
                    outputPath));
                continue;
            }

            workItems.Add(new TranscriptionWorkItem(input.FullPath, outputPath));
        }

        return new OutputPlan(workItems, outcomes);
    }

    private static string DetermineOutputPath(string inputPath, string? outputDir, string extension)
    {
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var directory = !string.IsNullOrEmpty(outputDir)
            ? outputDir
            : Path.GetDirectoryName(inputPath) ?? ".";

        return Path.Combine(directory, fileName + extension);
    }

    private static TranscriptionResult Failed(string sourceFile, string code, string message, string path) =>
        new(sourceFile, [], TranscriptionStatus.Failed, new TranscriptionError(TranscriptionStage.OutputPlanning, code, message, path));
}

public sealed record OutputPlan(
    IReadOnlyList<TranscriptionWorkItem> WorkItems,
    IReadOnlyList<TranscriptionResult> Outcomes)
{
    public bool IsSuccess => Outcomes.All(outcome => outcome.Status != TranscriptionStatus.Failed);
}
