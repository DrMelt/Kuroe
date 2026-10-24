using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Node.Llm;
using Kuroe.Tool;

namespace Kuroe.Workflows.BuiltinNodes;

/// <summary>内置节点定义：随程序注册进节点库，可被文件定义的同类节点同名覆盖。</summary>
internal static class BuiltinNodes
{
    /// <summary>内置容器成员「实施」的模型槽位名，绑定由引用处的模型绑定提供。</summary>
    internal static ModelRef ExecutorSlot { get; } = new("执行者");

    /// <summary>规划节点的计划输出端口名，也是容器承接计划的输入端口名。</summary>
    internal static PortName PlanPort { get; } = new("计划");

    /// <summary>规划节点承接任务目标的输入端口名。</summary>
    internal static PortName GoalPort { get; } = new("目标");

    /// <summary>执行节点承接计划的输入端口名。</summary>
    internal static PortName TaskPort { get; } = new("任务");

    /// <summary>执行节点：把目标写成一份计划。</summary>
    private static readonly NodeSpec PlanNodeDefinition = NodeSpec.Define(new NodeName("规划")) with
    {
        Type = LlmNodeTypes.Create(new LlmParameters
        {
            Prompt = "把目标写成可实施的计划，逐项给出要做什么与验收标准。",
        }),
        Inputs = [new InputPort(GoalPort, PortTypes.Text, "要规划的任务目标。")],
        Outputs = [new Port(PlanPort, PortTypes.Text, "目标对应的实施计划。")],
        Spec = new ExecutableSpec(),
    };

    /// <summary>执行节点：按计划实施。</summary>
    private static readonly NodeSpec ExecuteNodeDefinition = NodeSpec.Define(new NodeName("执行")) with
    {
        Type = LlmNodeTypes.Create(new LlmParameters
        {
            Tools = [new ToolPath("GetLocalTime")],
        }),
        Inputs = [new InputPort(TaskPort, PortTypes.Text, "待实施的计划，来自规划节点。")],
        Spec = new ExecutableSpec(),
    };

    /// <summary>交付容器：接一份计划，由成员按计划实施。实施节点的模型是槽位名，由引用处的模型绑定提供。</summary>
    private static readonly NodeSpec DeliveryContainerDefinition = NodeSpec.Define(new NodeName("交付")) with
    {
        Type = ContainerNodeTypes.Type,
        Inputs = [new InputPort(PlanPort, PortTypes.Text, "待实施的计划，来自规划节点。")],
        ModelSlots = [ExecutorSlot],
        Nodes =
        [
            NodeSpec.Reference(new NodeLabel("实施"), new NodeName("执行")) with
            {
                Model = ExecutorSlot,
                From = [new FromGroup([new FromLink(TaskPort, PortRef.Bound(PlanPort))])],
            },
        ],
    };

    /// <summary>内置节点定义集合，装库时按名并入。</summary>
    internal static IReadOnlyList<NodeSpec> Nodes { get; } =
        [PlanNodeDefinition, ExecuteNodeDefinition, DeliveryContainerDefinition];
}