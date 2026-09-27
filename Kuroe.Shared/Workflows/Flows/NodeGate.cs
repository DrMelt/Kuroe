namespace Kuroe.Shared.Workflows.Flows;

/// <summary>执行节点产出后是否等人放行。</summary>
public enum NodeGate
{
    /// <summary>产出即开下一执行节点。</summary>
    Auto,

    /// <summary>停在等人批准。</summary>
    Review,
}