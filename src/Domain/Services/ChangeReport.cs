using System.Text;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Services;

/// <summary>
/// Renders the corrections as a list for the person who reviews the subtitles. The model's corrections are
/// likely, not certain, so the list puts the kinds most worth a look first.
/// </summary>
public static class ChangeReport
{
    public static string Format(IReadOnlyList<SegmentChange> changes)
    {
        var report = new StringBuilder();
        report.AppendLine($"{changes.Count} corrections, the ones most worth checking first. Each shows the text before and after; | separates cues.");
        report.AppendLine("The reason given for a correction is the model's own and can be wrong.");

        foreach (var group in changes.GroupBy(change => change.Kind).OrderBy(group => group.Key))
        {
            report.AppendLine().AppendLine($"== {Heading(group.Key)} ({group.Count()}) ==");
            foreach (var change in group.OrderBy(change => change.Start))
            {
                report.AppendLine($"[{SubtitleFormatter.Clock(change.Start)}] {string.Join(" | ", change.Before)}");
                report.AppendLine($"     => {(change.After.Count == 0 ? "(deleted)" : string.Join(" | ", change.After))}");
            }
        }

        return report.ToString();
    }

    private static string Heading(ChangeKind kind) => kind switch
    {
        ChangeKind.Guess => "Guesses: a word changed on the model's own judgement of what was said",
        ChangeKind.Hallucination => "Deletions: text judged to be invented by the recognizer",
        ChangeKind.Term => "Terms: rewritten to match the glossary",
        ChangeKind.Segmentation => "Segmentation only: cues merged, split or re-cut, words unchanged",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
