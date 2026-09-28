namespace Kuroe.Shared.Workflows.Flows;

/// <summary>容器节点：组织一组有序子节点，为子节点提供作用域与展示路径。容器自身不执行，
/// 成员产出齐备时作汇合点，声明 Gate 时齐备后停人等批准。</summary>
public sealed record ContainerNode : NodeSpec
{
    /// <summary>有序子节点，至少一个。</summary>
    public required IReadOnlyList<NodeSpec> Nodes { get; init; }
}