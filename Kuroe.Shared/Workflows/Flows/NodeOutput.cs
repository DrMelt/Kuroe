namespace Kuroe.Shared.Workflows.Flows;

/// <summary>执行节点的产出契约：交回什么。执行引擎据此决定收口方式与契约工具。</summary>
public enum NodeOutput
{
    /// <summary>文本产出。</summary>
    Text,

    /// <summary>拆分目标，交回可独立实施的条目列表。</summary>
    Plan,
}