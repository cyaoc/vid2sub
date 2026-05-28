using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Workflow;
using Vid2Sub.WorkflowMcp;

namespace Vid2Sub.Tests.Workflow;

public sealed class WorkflowToolsTests
{
    [Fact]
    public async Task WriteSubtitles_ReturnsFailureForUnsupportedFormat()
    {
        using var temp = TestDirectory.Create();
        var outputRoot = temp.CreateDirectory("out");
        var tools = new WorkflowTools(new WorkbookInspector(), new GlossaryReader(), new Vid2SubProcessRunner());

        var result = await tools.WriteSubtitles(
            Path.Combine(outputRoot, "subtitle.bad"),
            outputRoot,
            "bad-format",
            [new TranscriptionSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "text")],
            overwriteConfirmed: false);

        Assert.Equal(ToolStatus.Failed, result.Status);
        Assert.Equal(WorkflowErrorCodes.UnsupportedFormat, result.Code);
    }
}
