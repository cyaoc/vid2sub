using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Audio;

namespace Vid2Sub.Tests.Infrastructure;

public sealed class FFmpegAudioProcessorTests
{
    [Fact]
    public void CreateStartInfo_UsesArgumentList()
    {
        var processor = new FFmpegAudioProcessor(new ResolvedEnvironmentConfiguration(
            FfmpegPath: "ffmpeg",
            TempDir: "/tmp/vid2sub"));

        var startInfo = processor.CreateStartInfo("input file.mp4", "output file.wav");

        Assert.Empty(startInfo.Arguments);
        Assert.Contains("input file.mp4", startInfo.ArgumentList);
        Assert.Contains("output file.wav", startInfo.ArgumentList);
        Assert.Contains("16000", startInfo.ArgumentList);
        Assert.Contains("1", startInfo.ArgumentList);
        Assert.Contains("pcm_s16le", startInfo.ArgumentList);
    }
}
