using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>流程图上展平后的容器：直接成员与直接子容器的组织单元，自身不执行。
/// 容器是汇合点：成员产出齐备时容器可被上下游消费。
/// 输出端口经 <see cref="OutputBindings"/> 映射到内部来源，运行时按表解析成员产出。</summary>
public sealed record ContainerNode : GraphNode
{
    /// <summary>节点类型的描述实例。</summary>
    public required INodeType Type { get; init; }

    /// <summary>直接子节点：成员与子容器，先根序。层级关系由它给出，标签前缀不承载层级。</summary>
    public required IReadOnlyList<GraphNode> Children { get; init; }

    /// <summary>容器输入端口表。</summary>
    public override IReadOnlyList<InputPort> Inputs { get; init; } = [];

    /// <summary>容器输出端口表。</summary>
    public override IReadOnlyList<Port> Outputs { get; init; } = [];

    /// <summary>输出端口到子树内成员端口的映射，编译期解析成成员路径。未声明来源时为空。</summary>
    public override IReadOnlyDictionary<PortName, NodePort>? OutputBindings { get; init; }

    /// <summary>输入侧方案：一组绑定覆盖全部直接子节点。</summary>
    public NodeInputPlan Plan => NodeInputPlan.Of(Children);
}
