using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Node.Shared.StaticGraph.Graph;

/// <summary>节点输入侧的编译期方案：产出模式、启动组、静态启动条件与执行次数上限。
/// 方案在编译期一次算出，就绪判定由运行期读取它，不再反复扫图。</summary>
public sealed record NodeInputPlan
{
    /// <summary>启动组，按组号排序；容器为成员与门控输入的单一组。</summary>
    public IReadOnlyList<InputGroup> Groups { get; init; } = [];

    /// <summary>容器发布内容的来源绑定，即直接成员；执行节点为空。</summary>
    public IReadOnlyList<InputBinding> ContentBindings { get; init; } = [];

    /// <summary>执行或发布次数上限。</summary>
    public int MaxExecutions { get; init; } = ExecutableNode.DefaultMaxExecutions;

    /// <summary>产出模式：逐次发布或首次发布后冻结。</summary>
    public NodeEmission Emission { get; init; } = NodeEmission.Repeated;

    /// <summary>静态启动条件：绑定的运行期常量产出未发布时节点不启动。</summary>
    public IReadOnlyList<InputBinding> StaticBindings { get; init; } = [];

    /// <summary>启动方式：动态来源驱动、静态输入就绪后执行一次，或由外部输入启动。</summary>
    public NodeStartMode StartMode { get; init; } = NodeStartMode.Driven;

    /// <summary>是否全部输入静态且没有动态启动组，静态输入就绪后执行一次。</summary>
    public bool StaticOnly => StartMode == NodeStartMode.StaticOnly;

    /// <summary>执行节点的方案：驱动绑定按组聚合，全部输入静态且没有入边时静态输入就绪后执行一次。</summary>
    public static NodeInputPlan Of(
        ExecutableSpec spec,
        IReadOnlyList<InputBinding> incoming,
        IReadOnlyList<InputPort> inputs,
        IReadOnlyList<InputBinding> staticBindings) => new()
        {
            Groups =
        [
            .. incoming
                .Where(binding => binding.Drives)
                .GroupBy(binding => binding.Group)
                .OrderBy(group => group.Key)
                .Select(group => new InputGroup([.. group])),
        ],
            MaxExecutions = spec.MaxExecutions ?? ExecutableNode.DefaultMaxExecutions,
            Emission = spec.Emission,
            StaticBindings = staticBindings,
            StartMode = incoming.Count == 0
            && inputs.All(port => port.IsStatic)
                ? NodeStartMode.StaticOnly
                : NodeStartMode.Driven,
        };

    /// <summary>容器的方案：一个启动组包含全部直接子节点与门控输入。成员驱动发布，门控输入只参与齐备与抑制。
    /// 发布内容的来源单独给出，门控输入不决定已发布内容是否可消费。</summary>
    public static NodeInputPlan Of(ContainerNode container)
    {
        IReadOnlyList<InputBinding> members =
            [.. container.Children.Select(child => InputBinding.Member(container.Path, child.Path))];

        return new()
        {
            Groups =
            [
                new InputGroup(
                [
                    .. members,
                    .. container.InputBindings?.Values.Where(binding => binding.Role == BindingRole.Gate) ?? [],
                ]),
            ],
            ContentBindings = members,
            MaxExecutions = container.MaxExecutions,
        };
    }

    /// <summary>由外部输入启动的节点方案：没有来源组与静态条件，等待型节点由外部交付激活。</summary>
    public static NodeInputPlan External() => new()
    {
        MaxExecutions = int.MaxValue,
        StartMode = NodeStartMode.External,
    };
}