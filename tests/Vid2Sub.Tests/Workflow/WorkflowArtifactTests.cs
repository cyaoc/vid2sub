using System.Text.Json;
using Vid2Sub.Infrastructure.Workflow;

namespace Vid2Sub.Tests.Workflow;

public sealed class WorkflowArtifactTests
{
    [Fact]
    public async Task WorkflowArtifacts_WritesJsonInsideOutputRoot()
    {
        using var temp = TestDirectory.Create();
        var outputRoot = temp.CreateDirectory("out");
        var path = Path.Combine(outputRoot, "workflow.manifest.json");

        var result = await WorkflowArtifacts.WriteJsonAsync(
            path,
            outputRoot,
            overwriteConfirmed: false,
            new WorkflowManifest(
                InputPath: "video.mp4",
                WorkbookPath: "glossary.xlsx",
                OutputRoot: outputRoot,
                Outputs: ["video.raw.ja.srt"],
                Warnings: []));

        Assert.Equal(ToolStatus.Success, result.Status);
        Assert.True(File.Exists(path));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("video.mp4", document.RootElement.GetProperty("inputPath").GetString());
    }

    [Fact]
    public void Vid2SubProcessRunner_UsesArgumentListForPathsWithSpaces()
    {
        var request = new RunVid2SubRequest(
            ExecutablePath: "/apps/vid2sub",
            InputPath: "/tmp/my video.mp4",
            OutputRoot: "/tmp/out dir",
            OutputPath: "/tmp/out dir/my video.raw.ja.srt",
            Format: "srt",
            Language: "ja",
            Model: "Base",
            OverwriteConfirmed: true);

        var startInfo = Vid2SubProcessRunner.CreateStartInfo(request);

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.Contains("/tmp/my video.mp4", startInfo.ArgumentList);
        Assert.Contains("/tmp/out dir", startInfo.ArgumentList);
        Assert.Contains("--overwrite", startInfo.ArgumentList);
    }
}
