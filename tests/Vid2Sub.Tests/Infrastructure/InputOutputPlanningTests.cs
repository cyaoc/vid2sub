using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Files;

namespace Vid2Sub.Tests.Infrastructure;

public sealed class InputOutputPlanningTests
{
    [Fact]
    public void InputFileCollector_ReportsMissingAndDuplicateInputs()
    {
        using var temp = TestDirectory.Create();
        var input = temp.WriteFile("video.mp4", "media");
        var missing = Path.Combine(temp.Root, "missing.mp4");

        var result = new InputFileCollector().Collect([input, missing, input]);

        Assert.Single(result.Files);
        Assert.Contains(result.Outcomes, o => o.Status == TranscriptionStatus.Failed && o.SourceFile == missing);
        Assert.Contains(result.Outcomes, o => o.Status == TranscriptionStatus.Skipped && o.SourceFile == input);
    }

    [Fact]
    public void OutputPlanner_FailsPlannedOutputCollisionsBeforeProcessing()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("a/video.mp4", "media");
        var second = temp.WriteFile("b/video.mp4", "media");
        var outputDir = temp.CreateDirectory("out");
        var collected = new InputFileCollector().Collect([first, second]);
        var config = TestConfigurations.Resolved(outputDir: outputDir);

        var plan = new OutputPlanner().CreatePlan(collected, config);

        Assert.False(plan.IsSuccess);
        Assert.Contains(plan.Outcomes, o => o.Status == TranscriptionStatus.Failed && o.Error?.Code == TranscriptionErrorCodes.OutputCollision);
    }

    [Fact]
    public void OutputPlanner_RequiresOverwriteForExistingOutput()
    {
        using var temp = TestDirectory.Create();
        var input = temp.WriteFile("video.mp4", "media");
        temp.WriteFile("video.vtt", "existing");
        var collected = new InputFileCollector().Collect([input]);
        var config = TestConfigurations.Resolved(overwrite: false);

        var plan = new OutputPlanner().CreatePlan(collected, config);

        Assert.False(plan.IsSuccess);
        Assert.Contains(plan.Outcomes, o => o.Error?.Code == TranscriptionErrorCodes.OutputExists);
    }
}
