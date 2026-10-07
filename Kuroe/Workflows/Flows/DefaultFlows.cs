using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>内置节点库与内置流程：用户层未指定默认流程时使用，工作目录 `.kuroe/` 里还没有 flows.json 时也只有这一条。
/// 模型选择都不写模型名，流程可加载但执行时无法确定模型，启动 run 时报错。</summary>
internal static class DefaultFlows
{
    /// <summary>内置流程的名字。</summary>
    internal static FlowName Name { get; } = new("默认");

    /// <summary>规划者：交回条目拆分。</summary>
    private static readonly ModelDefinition Planner = new() { Name = new ModelRef("规划者") };

    /// <summary>执行者：实施条目。</summary>
    private static readonly ModelDefinition Executor = new() { Name = new ModelRef("执行者") };

    /// <summary>规划执行节点：把目标拆成条目。</summary>
    private static readonly NodeSpec PlanNodeDefinition = new()
    {
        Name = new NodeName("规划"),
        Execution = new ExecutableSpec
        {
            Output = NodeOutput.Plan,
            Prompt = "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。",
        },
    };

    /// <summary>执行执行节点：按条目实施。</summary>
    private static readonly NodeSpec ExecuteNodeDefinition = new()
    {
        Name = new NodeName("执行"),
        Execution = new ExecutableSpec
        {
            Tools = [new ToolPath("GetLocalTime")],
            Mode = NodeMode.PerItem,
        },
    };

    /// <summary>交付容器：出一条计划，按条目实施。实施节点的模型是槽位名，由引用处的模型绑定提供。</summary>
    private static readonly NodeSpec DeliveryContainerDefinition = new()
    {
        Name = new NodeName("交付"),
        Inputs = [new PortName("计划")],
        Nodes =
        [
            new NodeSpec
            {
                Name = new NodeName("实施"),
                Use = new NodeName("执行"),
                Model = Executor.Name,
                From = [new NodeName("@计划")],
            },
        ],
    };

    /// <summary>内置内容：根容器包住规划与交付。装配层从外部输入模型，规划引执行叶子写模型选择名，交付绑定组内实施节点的模型槽位。</summary>
    internal static readonly FlowFile Builtin = new(
        [PlanNodeDefinition, ExecuteNodeDefinition, DeliveryContainerDefinition],
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
                            In = new Dictionary<PortName, NodeName> { [new PortName("计划")] = new NodeName("制定计划") },
                            Models = new Dictionary<ModelRef, ModelRef> { [Executor.Name] = Executor.Name },
                        },
                    ],
                }),
        ]);
}
