using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Configuration;

namespace Vid2Sub.Tests;

public class ConfigurationTests
{
    [Fact]
    public void Yaml_values_replace_defaults_and_removed_keys_are_ignored()
    {
        var config = YamlConfigLoader.Parse("""
            model:
              type: "medium"
              storage_dir: "/data/models"
            inference:
              language: ja
              threads: 4
              beam_size: 3
            environment:
              ffmpeg_path: "ffmpeg"
              temp_dir: "./temp"
            output:
              format: srt
              verbose: true
            """);

        Assert.Equal(ModelType.Medium, config.Model.Type);
        Assert.Equal("/data/models", config.Model.StorageDir);
        Assert.Equal("ja", config.Inference.Language);
        Assert.Equal(4, config.Inference.Threads);
        Assert.Equal(3, config.Inference.BeamSize);
        Assert.Equal(OutputFormat.Srt, config.Output.Format);
        Assert.True(config.Output.Verbose);
    }

    [Theory]
    [InlineData("")]
    [InlineData("model:\noutput:\n")]
    public void Missing_or_empty_sections_fall_back_to_defaults(string yaml)
    {
        var config = YamlConfigLoader.Parse(yaml);

        Assert.Equal(ModelType.LargeV3Turbo, config.Model.Type);
        Assert.Equal("auto", config.Inference.Language);
        Assert.Equal(5, config.Inference.BeamSize);
        Assert.Equal(OutputFormat.Vtt, config.Output.Format);
        Assert.False(config.Output.Verbose);
    }

    [Fact]
    public void Unknown_enum_value_is_reported_as_invalid_config() =>
        Assert.Throws<InvalidDataException>(() => YamlConfigLoader.Parse("output:\n  format: docx\n"));

    [Fact]
    public void Command_line_overrides_yaml_only_where_given()
    {
        var config = YamlConfigLoader.Parse("inference:\n  language: ja\n  threads: 4\noutput:\n  format: srt\n");

        config.ApplyOverrides(format: OutputFormat.Text, model: ModelType.Tiny);

        Assert.Equal(OutputFormat.Text, config.Output.Format); // command line wins
        Assert.Equal(ModelType.Tiny, config.Model.Type);
        Assert.Equal("ja", config.Inference.Language);         // YAML kept where the command line is silent
        Assert.Equal(4, config.Inference.Threads);
    }

    [Fact]
    public void Verbose_in_yaml_survives_a_command_line_without_the_flag()
    {
        var config = YamlConfigLoader.Parse("output:\n  verbose: true\n");

        config.ApplyOverrides(verbose: false);

        Assert.True(config.Output.Verbose);
    }

    [Fact]
    public void Negative_threads_are_rejected() =>
        Assert.Throws<ArgumentException>(() => new AppConfiguration().ApplyOverrides(threads: -1));

    [Fact]
    public void Beam_size_below_one_is_rejected() =>
        Assert.Throws<ArgumentException>(() => YamlConfigLoader.Parse("inference:\n  beam_size: 0\n").ApplyOverrides());

    [Fact]
    public async Task Explicit_config_path_must_exist() =>
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => YamlConfigLoader.LoadAsync("no-such-config.yaml", TestContext.Current.CancellationToken));

    [Fact]
    public async Task Relative_model_dir_is_anchored_to_the_executable()
    {
        using var temp = new TempDirectory();
        var path = temp.File("custom.yaml", "model:\n  storage_dir: ./models\n");

        var config = await YamlConfigLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath("models", AppContext.BaseDirectory), config.Model.StorageDir);
    }
}
