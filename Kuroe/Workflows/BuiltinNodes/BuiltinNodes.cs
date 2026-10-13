using Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Workflows.BuiltinNodes;

/// <summary>内置节点定义：随程序注册进节点库，可被文件定义的同类节点同名覆盖。</summary>
internal static class BuiltinNodes
{
    /// <summary>内置容器成员「实施」的模型槽位名，绑定由引用处的模型绑定提供。</summary>
    internal static ModelRef ExecutorSlot { get; } = new("执行者");

    /// <summary>规划节点的拆分支票端口名。</summary>
    internal static PortName SplitPort { get; } = new("拆分");

    /// <summary>规划节点承接任务目标的输入端口名。</summary>
    internal static PortName GoalPort { get; } = new("目标");

    /// <summary>执行节点承接待实施条目的输入端口名。</summary>
    internal static PortName TaskPort { get; } = new("任务");

    /// <summary>交付容器的传入端口名。</summary>
    internal static PortName PlanPort { get; } = new("计划");

    /// <summary>执行节点：把目标拆成条目。</summary>
    private static readonly NodeSpec PlanNodeDefinition = new()
    {
        Name = new NodeName("规划"),
        Kind = NodeKind.Executable,
        Inputs = [new InputPort(GoalPort, PortType.Text, "要拆分的任务目标。")],
        Outputs = [new Port(SplitPort, PortType.WorkItems, "目标拆成的条目清单。")],
        Execution = new ExecutableSpec
        {
            Prompt = "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。",
        },
    };

    /// <summary>执行节点：按条目实施。</summary>
    private static readonly NodeSpec ExecuteNodeDefinition = new()
    {
        Name = new NodeName("执行"),
        Kind = NodeKind.Executable,
        Inputs = [new InputPort(TaskPort, PortType.Text, "待实施的条目，来自拆分来源。")],
        Execution = new ExecutableSpec
        {
            Tools = [new CapabilityPath("GetLocalTime")],
            Mode = NodeMode.PerItem,
        },
    };

    /// <summary>交付容器：出一条计划，按条目实施。实施节点的模型是槽位名，由引用处的模型绑定提供。</summary>
    private static readonly NodeSpec DeliveryContainerDefinition = new()
    {
        Name = new NodeName("交付"),
        Kind = NodeKind.Container,
        Inputs = [new InputPort(PlanPort, PortType.Text, "待实施的条目拆分，来自规划节点。")],
        ModelSlots = [ExecutorSlot],
        Nodes =
        [
            new NodeSpec
            {
                Name = new NodeName("实施"),
                Kind = null,
                Use = new NodeName("执行"),
                Model = ExecutorSlot,
                From = [new FromGroup([new FromLink(TaskPort, PortRef.Bound(PlanPort))])],
            },
        ],
    };

    /// <summary>内置节点定义集合，装库时按名并入。</summary>
    internal static IReadOnlyList<NodeSpec> Nodes { get; } =
        [PlanNodeDefinition, ExecuteNodeDefinition, DeliveryContainerDefinition];
}
