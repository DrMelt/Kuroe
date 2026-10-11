using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Nodes.Builtin;

/// <summary>内置节点定义：随程序注册进节点库，可被文件定义的同类节点同名覆盖。</summary>
internal static class BuiltinNodes
{
    /// <summary>内置容器成员「实施」的模型槽位名，绑定由引用处的模型绑定提供。</summary>
    internal static ModelRef ExecutorSlot { get; } = new("执行者");

    /// <summary>规划节点的拆分支票端口名。</summary>
    internal static PortName SplitPort { get; } = new("拆分");

    /// <summary>执行节点：把目标拆成条目。</summary>
    private static readonly NodeSpec PlanNodeDefinition = new()
    {
        Name = new NodeName("规划"),
        Kind = NodeKind.Executable,
        Outputs = [new Port(SplitPort, PortKind.Items, "目标拆成的条目清单。")],
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
        Kind = NodeKind.Container,
        Inputs = [new ContainerInput(new PortName("计划"), "待实施的条目拆分，来自规划节点。")],
        ModelSlots = [ExecutorSlot],
        Nodes =
        [
            new NodeSpec
            {
                Name = new NodeName("实施"),
                Kind = null,
                Use = new NodeName("执行"),
                Model = ExecutorSlot,
                From = [new SourceRef(PortRef.Bound(new PortName("计划")))],
            },
        ],
    };

    /// <summary>内置节点定义集合，装库时按名并入。</summary>
    internal static IReadOnlyList<NodeSpec> Nodes { get; } =
        [PlanNodeDefinition, ExecuteNodeDefinition, DeliveryContainerDefinition];
}
