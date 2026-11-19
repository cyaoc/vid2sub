using Vid2Sub.Domain.Enums;

namespace Vid2Sub.Domain.Models;

/// <summary>One applied correction, kept for the person who reviews the subtitles: where, why, and the text before and after.</summary>
public sealed record SegmentChange(TimeSpan Start, ChangeKind Kind, IReadOnlyList<string> Before, IReadOnlyList<string> After);
