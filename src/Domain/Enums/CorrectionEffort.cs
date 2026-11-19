namespace Vid2Sub.Domain.Enums;

/// <summary>
/// How hard the model thinks while proofreading: the levels of <c>claude --effort</c>.
/// Higher is slower and uses more of the Claude usage limit.
/// </summary>
public enum CorrectionEffort
{
    Low,
    Medium,
    High,
    XHigh,
    Max
}
