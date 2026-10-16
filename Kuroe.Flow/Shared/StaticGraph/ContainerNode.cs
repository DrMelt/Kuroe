namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>流程图上展平后的容器：直接成员执行节点与直接子容器的组织单元，自身不执行。
/// 容器是汇合点：成员产出齐备时容器可被上下游消费。
/// 命名的输出端口经 <see cref="PortBindings"/> 映射到子树内成员引用，运行时按表解析成员产出。</summary>
public sealed record ContainerNode : GraphNode
{
    /// <summary>节点类型的描述实例。</summary>
    public required INodeType Type { get; init; }

    /// <summary>直接成员执行节点，先根序。</summary>
    public required IReadOnlyList<int> Members { get; init; }

    /// <summary>直接子容器，先根序。</summary>
    public required IReadOnlyList<int> SubContainers { get; init; }

    /// <summary>命名输出端口到子树内成员引用的映射，值可写 成员名@端口 取成员命名段。未声明时为空。</summary>
    public required IReadOnlyDictionary<PortName, PortRef>? PortBindings { get; init; }
}
