using Vid2Sub.Domain.Enums;

namespace Vid2Sub.Domain.Models;

/// <summary>
/// Replaces segments <paramref name="From"/> to <paramref name="To"/> (1-based, inclusive) with one segment
/// per entry in <paramref name="Lines"/>; no lines deletes them.
/// </summary>
public sealed record SegmentEdit(int From, int To, IReadOnlyList<string> Lines, ChangeKind Kind = ChangeKind.Segmentation);
