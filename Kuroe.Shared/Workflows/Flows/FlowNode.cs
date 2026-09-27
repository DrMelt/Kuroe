namespace Kuroe.Shared.Workflows.Flows;

/// <summary>容器节点：组织一组有序子节点，为子节点提供作用域与展示路径。容器自身不执行。</summary>
public sealed record FlowNode : NodeSpec
{
    /// <summary>有序子节点，至少一个。</summary>
    public required IReadOnlyList<NodeSpec> Nodes { get; init; }
}