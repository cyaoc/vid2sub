namespace Vid2Sub.Domain.Enums;

/// <summary>
/// Why a correction was made, as the model reports it. Declared in the order a reviewer should check them:
/// the kinds most likely to be wrong come first.
/// </summary>
public enum ChangeKind
{
    /// <summary>A word changed on the model's own judgement of what was said.</summary>
    Guess,

    /// <summary>Text deleted because the model judged the recognizer invented it.</summary>
    Hallucination,

    /// <summary>A word rewritten to a spelling from the glossary.</summary>
    Term,

    /// <summary>Cue boundaries moved; the words are unchanged.</summary>
    Segmentation
}
