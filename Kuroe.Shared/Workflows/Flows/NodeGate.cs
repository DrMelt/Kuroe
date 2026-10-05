namespace Kuroe.Shared.Workflows.Flows;

/// <summary>执行节点产出后是否停在待批准。</summary>
public enum NodeGate
{
    /// <summary>产出即开下一执行节点。</summary>
    Auto,

    /// <summary>产出后停在待批准。</summary>
    Review,
}
