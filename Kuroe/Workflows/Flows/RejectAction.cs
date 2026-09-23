namespace Kuroe.Workflows.Flows;

/// <summary>检查不通过时的处置，只适用于检查步骤。</summary>
public enum RejectAction
{
    /// <summary>停下等人返工或放行。</summary>
    Stop,

    /// <summary>退回实施步骤，带检查意见再来一轮。</summary>
    Retry,
}

