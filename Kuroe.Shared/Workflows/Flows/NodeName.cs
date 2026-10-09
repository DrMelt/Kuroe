namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程图里的节点名。流程内唯一，展示即原文。</summary>
public readonly record struct NodeName(string Value)
{
    /// <summary>节点名的文本形式。</summary>
    public override string ToString() => Value;
}
