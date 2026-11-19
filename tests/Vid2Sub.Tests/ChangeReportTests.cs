using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;
using Vid2Sub.Domain.Services;

namespace Vid2Sub.Tests;

public class ChangeReportTests
{
    [Fact]
    public void Changes_are_grouped_by_reason_with_the_riskiest_first_and_in_time_order_within_a_group()
    {
        var report = ChangeReport.Format(
        [
            new(TimeSpan.FromSeconds(75), ChangeKind.Segmentation, ["a", "b"], ["a b"]),
            new(TimeSpan.FromSeconds(200), ChangeKind.Guess, ["later"], ["LATER"]),
            new(TimeSpan.FromSeconds(9), ChangeKind.Hallucination, ["thanks for watching"], []),
            new(TimeSpan.FromSeconds(3), ChangeKind.Term, ["再棒図"], ["サイボウズ"]),
            new(TimeSpan.FromSeconds(5), ChangeKind.Guess, ["sooner"], ["SOONER"]),
        ]).ReplaceLineEndings("\n");

        Assert.StartsWith("5 corrections", report);
        var positions = new[] { "== Guesses", "[00:05] sooner\n     => SOONER", "[03:20] later\n     => LATER", "== Deletions", "[00:09] thanks for watching\n     => (deleted)", "== Terms", "[00:03] 再棒図\n     => サイボウズ", "== Segmentation only", "[01:15] a | b\n     => a b" }
            .Select(text => report.IndexOf(text, StringComparison.Ordinal))
            .ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
    }

    [Fact]
    public void A_correction_without_changes_still_says_so() =>
        Assert.StartsWith("0 corrections", ChangeReport.Format([]));
}
