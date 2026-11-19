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
            correction:
              enabled: true
              model: "claude-sonnet-5-5"
              effort: xhigh
              glossary: "/data/terms.xlsx"
              glossary_column: ja
            """);

        Assert.Equal(ModelType.Medium, config.Model.Type);
        Assert.Equal("/data/models", config.Model.StorageDir);
        Assert.Equal("ja", config.Inference.Language);
        Assert.Equal(4, config.Inference.Threads);
        Assert.Equal(3, config.Inference.BeamSize);
        Assert.Equal(OutputFormat.Srt, config.Output.Format);
        Assert.True(config.Output.Verbose);
        Assert.True(config.Correction.Enabled);
        Assert.Equal("claude-sonnet-5-5", config.Correction.Model);
        Assert.Equal(CorrectionEffort.XHigh, config.Correction.Effort);
        Assert.Equal("/data/terms.xlsx", config.Correction.Glossary);
        Assert.Equal("ja", config.Correction.GlossaryColumn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("model:\noutput:\ncorrection:\n")]
    public void Missing_or_empty_sections_fall_back_to_defaults(string yaml)
    {
        var config = YamlConfigLoader.Parse(yaml);

        Assert.Equal(ModelType.LargeV3Turbo, config.Model.Type);
        Assert.Equal("auto", config.Inference.Language);
        Assert.Equal(5, config.Inference.BeamSize);
        Assert.Equal(OutputFormat.Vtt, config.Output.Format);
        Assert.False(config.Output.Verbose);
        Assert.False(config.Correction.Enabled);
        Assert.Equal("claude-opus-5-5", config.Correction.Model);
        Assert.Equal(CorrectionEffort.Medium, config.Correction.Effort);
        Assert.Null(config.Correction.Glossary);
        Assert.Null(config.Correction.GlossaryColumn);
    }

    [Theory]
    [InlineData("output:\n  format: docx\n")]
    [InlineData("correction:\n  effort: extreme\n")]
    public void Unknown_enum_value_is_reported_as_invalid_config(string yaml) =>
        Assert.Throws<InvalidDataException>(() => YamlConfigLoader.Parse(yaml));

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
    public void A_glossary_on_the_command_line_replaces_the_yaml_one_and_turns_correction_on()
    {
        var config = YamlConfigLoader.Parse("correction:\n  glossary: /data/yaml.xlsx\n  glossary_column: ja\n");

        config.ApplyOverrides(glossary: "/data/cli.xlsx", glossaryColumn: "zh");

        Assert.True(config.Correction.Enabled);
        Assert.Equal("/data/cli.xlsx", config.Correction.Glossary);
        Assert.Equal("zh", config.Correction.GlossaryColumn);
    }

    [Theory]
    [InlineData("correction:\n  enabled: true\n  glossary: /data/yaml.xlsx\n  glossary_column: ja\n", true)]
    [InlineData("correction:\n  glossary: /data/yaml.xlsx\n  glossary_column: ja\n", false)]
    public void Without_a_glossary_on_the_command_line_correction_follows_yaml(string yaml, bool expected)
    {
        var config = YamlConfigLoader.Parse(yaml);

        config.ApplyOverrides();

        Assert.Equal(expected, config.Correction.Enabled);
        Assert.Equal("/data/yaml.xlsx", config.Correction.Glossary);
        Assert.Equal("ja", config.Correction.GlossaryColumn);
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
    public async Task Relative_model_dir_and_glossary_are_anchored_to_the_executable()
    {
        using var temp = new TempDirectory();
        var path = temp.File("custom.yaml", "model:\n  storage_dir: ./models\ncorrection:\n  glossary: terms.xlsx\n");

        var config = await YamlConfigLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath("models", AppContext.BaseDirectory), config.Model.StorageDir);
        Assert.Equal(Path.GetFullPath("terms.xlsx", AppContext.BaseDirectory), config.Correction.Glossary);
    }
}
