namespace Kuroe.Shared.Workflows.Flows;

/// <summary>容器节点：组织一组有序子节点，为子节点提供作用域并聚合它们的产出。
/// 容器自身不执行，运行时展开为叶子序列。并行容器把子叶子作为分支，按分配方案并行推进。</summary>
public sealed record FlowNode : NodeSpec
{
    /// <summary>有序子节点，至少一个。</summary>
    public required IReadOnlyList<NodeSpec> Nodes { get; init; }

    /// <summary>容器模式：顺序容器只提供作用域，并行容器的子叶子是并行分支。</summary>
    public ContainerMode Mode { get; init; } = ContainerMode.Sequential;
}