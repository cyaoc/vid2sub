using Vid2Sub.Infrastructure.Workflow;

namespace Vid2Sub.Tests.Workflow;

public sealed class WorkflowToolingTests
{
    [Fact]
    public void ToolResult_FailureCarriesStableErrorCode()
    {
        var result = ToolResult<string>.Failure("path_outside_scope", "Path is outside output root", "/tmp/outside.srt");

        Assert.Equal(ToolStatus.Failed, result.Status);
        Assert.Equal("path_outside_scope", result.Code);
        Assert.Null(result.Data);
        Assert.Equal("/tmp/outside.srt", result.Details);
    }

    [Fact]
    public void PathScopeGuard_RejectsWritesOutsideOutputRoot()
    {
        using var temp = TestDirectory.Create();
        var outputRoot = temp.CreateDirectory("out");
        var outside = Path.Combine(temp.Root, "elsewhere", "subtitle.srt");

        var result = PathScopeGuard.ValidateWriteTarget(outside, outputRoot, overwriteConfirmed: true);

        Assert.Equal(ToolStatus.Failed, result.Status);
        Assert.Equal(WorkflowErrorCodes.PathOutsideScope, result.Code);
    }

    [Fact]
    public void PathScopeGuard_RequiresOverwriteConfirmation()
    {
        using var temp = TestDirectory.Create();
        var outputRoot = temp.CreateDirectory("out");
        var existing = temp.WriteFile("out/subtitle.srt", "existing");

        var result = PathScopeGuard.ValidateWriteTarget(existing, outputRoot, overwriteConfirmed: false);

        Assert.Equal(ToolStatus.Failed, result.Status);
        Assert.Equal(WorkflowErrorCodes.OverwriteNotConfirmed, result.Code);
    }
}
