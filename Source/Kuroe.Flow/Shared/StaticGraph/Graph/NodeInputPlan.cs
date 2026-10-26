using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>输入侧的一条绑定：从来源节点的一个端口取值，进入本节点的一个输入端口。
/// 信号语义只以新版本驱动启动，不取内容；Trigger 版还携带触发值。</summary>
public sealed record InputBinding(
    NodePath Source,
    PortName? Port,
    PortType? Type = null,
    PortName? TargetInput = null)
{
    /// <summary>节点级绑定，用于容器按成员整体推进内容版本。</summary>
    public static InputBinding Node(NodePath source) => new(source, null);

    /// <summary>是否信号语义，组内有信号绑定时只认信号来源的新版本。</summary>
    public bool Signal => Type?.Semantics == PortSemantics.Signal;

    /// <summary>是否为 Trigger 版端口，启动组按该端口的触发值决定是否启动。</summary>
    public bool CarriesTrigger => Type is { } type && Ports.CarriesTrigger(type);

    /// <summary>已消费版本的键。</summary>
    public ConsumptionKey Key => new(Source, Port);

    /// <summary>这一绑定是否触发下游。普通端口与节点级绑定恒触发。</summary>
    public bool Triggers(ISourceState source) =>
        !CarriesTrigger || Port is not { } port || source.Triggers(port);
}

/// <summary>输入侧的一个启动组：组内绑定全部可用时组齐备，组间任一齐备即可启动。
/// 组内有信号绑定时只认信号来源的新版本，否则认组内任一边的新版本。</summary>
public sealed record InputGroup(IReadOnlyList<InputBinding> Bindings)
{
    /// <summary>组是否由信号来源驱动。</summary>
    public bool DrivenBySignals => Bindings.Any(binding => binding.Signal);

    /// <summary>组内的信号绑定。</summary>
    public IEnumerable<InputBinding> Signals => Bindings.Where(binding => binding.Signal);

    /// <summary>组内是否存在触发值为 false 的 Trigger 绑定。</summary>
    public bool Suppressed(IReadinessSources sources) =>
        Bindings.Any(binding => !binding.Triggers(sources.Source(binding.Source)));
}

/// <summary>节点输入侧的编译期方案：启动组与执行次数上限。
/// 方案在编译期由执行视图一次算出，判定只读它，不再反复扫图。</summary>
public sealed partial record NodeInputPlan
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
            new InputGroup([.. children.Select(child => InputBinding.Node(child.Path))]),
        ],
        MaxExecutions = int.MaxValue,
    };

    /// <summary>一条边在方案里的绑定：来源端口、目标输入端口与类型随边落定。</summary>
    private static InputBinding Binding(FlowEdge edge) =>
        new(edge.Source, edge.SourcePort, edge.Type, edge.TargetInput);
}
