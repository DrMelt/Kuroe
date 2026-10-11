using Kuroe.Nodes.Builtin;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>内置流程：用户层未指定默认流程时使用，工作目录 `.kuroe/` 里还没有 flows.json 时也只有这一条。
/// 模型选择都不写模型名，流程可加载但执行时无法确定模型，启动 run 时报错。节点定义见 BuiltinNodes。</summary>
internal static class DefaultFlows
{
    /// <summary>内置流程的名字。</summary>
    internal static FlowName Name { get; } = new("默认");

    /// <summary>规划者：交回条目拆分。</summary>
    private static readonly ModelDefinition Planner = new() { Name = new ModelRef("规划者") };

    /// <summary>执行者：实施条目。</summary>
    private static readonly ModelDefinition Executor = new() { Name = BuiltinNodes.ExecutorSlot };

    /// <summary>内置流程：根容器包住规划与交付。装配层从外部输入模型，规划引执行叶子写模型选择名，交付绑定组内实施节点的模型槽位。</summary>
    internal static readonly IReadOnlyList<FlowDefinition> Builtin =
    [
        new FlowDefinition(Name, "内置流程：制定计划、分配执行",
            [Planner, Executor],
            new NodeSpec
            {
                Name = new NodeName("整体"),
                Nodes =
                [
                    new NodeSpec { Name = new NodeName("制定计划"), Use = new NodeName("规划"), Model = Planner.Name },
                    new NodeSpec
                    {
                        Name = new NodeName("交付"),
                        Use = new NodeName("交付"),
                        In = new Dictionary<PortName, PortRef> { [new PortName("计划")] = PortRef.Named(new NodeName("制定计划"), PortNames.Split) },
                        Models = new Dictionary<ModelRef, ModelRef> { [Executor.Name] = Executor.Name },
                    },
                ],
            }),
    ];
}
