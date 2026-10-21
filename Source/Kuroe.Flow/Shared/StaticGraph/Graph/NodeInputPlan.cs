using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>输入侧的一条绑定：从来源取值。信号来源只以新版本驱动启动，不取内容。</summary>
public sealed record InputBinding(
    NodePath Source,
    bool Signal);

/// <summary>输入侧的一个启动组：组内绑定全部可用时组齐备，组间任一齐备即可启动。
/// 组内有信号绑定时只认信号来源的新版本，否则认组内任一边的新版本。</summary>
public sealed record InputGroup(IReadOnlyList<InputBinding> Bindings)
{
    /// <summary>组是否由信号来源驱动。</summary>
    public bool DrivenBySignals => Bindings.Any(binding => binding.Signal);

    /// <summary>组内的信号绑定。</summary>
    public IEnumerable<InputBinding> Signals => Bindings.Where(binding => binding.Signal);
}

/// <summary>节点输入侧的编译期方案：启动组与执行次数上限。
/// 方案在编译期由执行视图一次算出，判定只读它，不再反复扫图。</summary>
public sealed record NodeInputPlan
{
    /// <summary>启动组，按 From 的组序。</summary>
    public IReadOnlyList<InputGroup> Groups { get; init; } = [];

    /// <summary>执行次数上限。</summary>
    public int MaxExecutions { get; init; } = ExecutableNode.DefaultMaxExecutions;

    /// <summary>执行节点的方案：入边按组聚合。</summary>
    public static NodeInputPlan Of(ExecutableSpec spec, IReadOnlyList<FlowEdge> incoming) => new()
    {
        Groups =
        [
            .. incoming
                .GroupBy(edge => edge.Group)
                .OrderBy(group => group.Key)
                .Select(group => new InputGroup([.. group.Select(Binding)])),
        ],
        MaxExecutions = spec.MaxExecutions ?? ExecutableNode.DefaultMaxExecutions,
    };

    /// <summary>容器的方案：一组绑定覆盖全部直接子节点，成员产出齐备且版本有变化即推进内容版本。</summary>
    public static NodeInputPlan Of(IReadOnlyList<GraphNode> children) => new()
    {
        Groups =
        [
            new InputGroup([.. children.Select(child => new InputBinding(child.Path, false))]),
        ],
        MaxExecutions = int.MaxValue,
    };

    /// <summary>一条边在方案里的绑定：信号标注按输入类型。</summary>
    private static InputBinding Binding(FlowEdge edge) => new(edge.Source, edge.Type == PortType.Signal);
}
