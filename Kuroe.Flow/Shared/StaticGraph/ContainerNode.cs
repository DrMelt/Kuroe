namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>流程图上展平后的容器：直接成员执行节点与直接子容器的组织单元，自身不执行。
/// 容器是汇合点：成员产出齐备时容器可被上下游消费。
/// 命名的输出端口经 <see cref="PortBindings"/> 映射到子树内成员端口，运行时按表解析成员产出。</summary>
public sealed record ContainerNode : GraphNode
{
    /// <summary>节点类型的描述实例。</summary>
    public required INodeType Type { get; init; }

    /// <summary>直接子节点：成员执行节点与子容器，先根序。层级关系由它给出，标签前缀不承载层级。</summary>
    public required IReadOnlyList<GraphNode> Children { get; init; }

    /// <summary>命名输出端口到子树内成员端口的映射，编译期解析成成员路径。未声明时为空。</summary>
    public required IReadOnlyDictionary<PortName, NodePort>? PortBindings { get; init; }

    /// <summary>输入侧方案：一组绑定覆盖全部直接子节点。</summary>
    public NodeInputPlan Plan => NodeInputPlan.Of(Children);
}
