using Vid2Sub.Infrastructure.Correction;

namespace Vid2Sub.Tests;

public class CorrectionRulesTests
{
    private static string Shipped(string name) => File.ReadAllText(Path.Combine(LayeringTests.RepositoryRoot(), name));

    [Fact]
    public void The_rules_of_the_language_follow_the_common_ones()
    {
        using var temp = new TempDirectory();

        var (text, source) = CorrectionRules.Load(temp.Path, " JA ");

        Assert.Equal($"{Shipped("correction-rules.md").TrimEnd()}\n\n{Shipped("correction-rules.ja.md")}", text);
        Assert.Equal("built-in correction-rules.md + built-in correction-rules.ja.md", source);
    }

    [Fact]
    public void Files_beside_the_executable_replace_the_built_in_rules_one_by_one()
    {
        using var temp = new TempDirectory();
        temp.File("correction-rules.md", "my common rules\n");
        temp.File("correction-rules.zh.md", "my Chinese rules");

        Assert.Equal(("my common rules\n\nmy Chinese rules", "correction-rules.md + correction-rules.zh.md"), CorrectionRules.Load(temp.Path, "zh"));
        Assert.Equal("correction-rules.md + built-in correction-rules.ja.md", CorrectionRules.Load(temp.Path, "ja").Source);
    }

    [Theory]
    [InlineData("en", "built-in correction-rules.md (no rules for language \"en\")")]
    [InlineData(null, "built-in correction-rules.md (language not known, so no language rules)")]
    [InlineData("", "built-in correction-rules.md (language not known, so no language rules)")]
    // The code becomes part of a file name: one that could point elsewhere is not a language.
    [InlineData("../ja", "built-in correction-rules.md (language not known, so no language rules)")]
    public void A_language_without_rules_gets_the_common_ones(string? language, string expectedSource)
    {
        using var temp = new TempDirectory();
        temp.File("ja.md", "must not be picked up");

        Assert.Equal((Shipped("correction-rules.md"), expectedSource), CorrectionRules.Load(temp.Path, language));
    }
}
