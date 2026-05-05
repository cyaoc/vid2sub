using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Configuration;

namespace Vid2Sub.Tests.Configuration;

public sealed class ConfigurationResolverTests
{
    [Fact]
    public async Task ResolveAsync_AppliesCliOverridesOverYamlAndDefaults()
    {
        using var temp = TestDirectory.Create();
        var configPath = temp.WriteFile("config.yaml", """
            model:
              type: "Base"
              storage_dir: "./yaml-models"
            inference:
              language: "zh"
              threads: 2
              beam_size: 4
            environment:
              ffmpeg_path: "ffmpeg"
              temp_dir: "./yaml-temp"
            output:
              format: "srt"
              log_level: "debug"
            """);

        var resolver = new ConfigurationResolver(temp.Root, temp.Root);
        var result = await resolver.ResolveAsync(
            configPath,
            new CliOptions(
                Inputs: ["video.mp4"],
                Format: "text",
                Language: "en",
                Model: "Small",
                Threads: 8,
                LogLevel: "error"));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        var config = result.Configuration!;
        Assert.Equal(ModelType.Small, config.Model.Type);
        Assert.Equal(OutputFormat.Text, config.Output.Format);
        Assert.Equal("en", config.Inference.Language);
        Assert.Equal(8, config.Inference.Threads);
        Assert.Equal(Vid2SubLogLevel.Error, config.Output.LogLevel);
        Assert.Equal(Path.Combine(temp.Root, "yaml-models"), config.Model.StorageDir);
        Assert.Equal(Path.Combine(temp.Root, "yaml-temp"), config.Environment.TempDir);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsAllValidationErrors()
    {
        using var temp = TestDirectory.Create();
        var configPath = temp.WriteFile("config.yaml", """
            model:
              type: "not-a-model"
            inference:
              threads: -1
            output:
              format: "not-a-format"
              log_level: "chatty"
            """);

        var resolver = new ConfigurationResolver(temp.Root, temp.Root);
        var result = await resolver.ResolveAsync(configPath, new CliOptions(Inputs: ["video.mp4"]));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "model.type");
        Assert.Contains(result.Errors, e => e.Key == "inference.threads");
        Assert.Contains(result.Errors, e => e.Key == "output.format");
        Assert.Contains(result.Errors, e => e.Key == "output.log_level");
    }

    [Fact]
    public async Task ResolveAsync_RejectsPromptConfiguration()
    {
        using var temp = TestDirectory.Create();
        var configPath = temp.WriteFile("config.yaml", """
            inference:
              prompt: "@prompt.txt"
            """);

        var resolver = new ConfigurationResolver(temp.Root, temp.Root);
        var result = await resolver.ResolveAsync(configPath, new CliOptions(Inputs: ["video.mp4"]));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "inference.prompt");
    }

    [Fact]
    public async Task ResolveAsync_FailsWhenSelectedConfigDoesNotExist()
    {
        using var temp = TestDirectory.Create();
        var missingConfig = Path.Combine(temp.Root, "missing.yaml");

        var resolver = new ConfigurationResolver(temp.Root, temp.Root);
        var result = await resolver.ResolveAsync(missingConfig, new CliOptions(Inputs: ["video.mp4"]));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "config");
    }
}
