using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Interfaces;

/// <summary>Proofreads recognized segments: misrecognized words, hallucinated text and cue boundaries.</summary>
public interface ISubtitleCorrector
{
    /// <summary>
    /// Returns the corrected segments and what was changed, or throws when the correction cannot be completed.
    /// The caller still holds the recognized segments, so a finished transcription is never lost.
    /// </summary>
    Task<(IReadOnlyList<TranscriptionSegment> Segments, IReadOnlyList<SegmentChange> Changes)> CorrectAsync(
        IReadOnlyList<TranscriptionSegment> segments,
        CancellationToken cancellationToken = default);
}
