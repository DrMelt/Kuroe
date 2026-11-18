using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Node.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Kuroe.Node.Llm;
using Kuroe.Tool;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Flow.Hosting.Workflows.BuiltinNodes;

/// <summary>内置节点定义：随程序注册进节点库，可被文件定义的同类节点同名覆盖。</summary>
internal static class BuiltinNodes
{
    /// <summary>规划节点的计划输出端口名，也是容器承接计划的输入端口名。</summary>
    internal static PortName PlanPort { get; } = new("计划");

    /// <summary>规划节点承接任务目标的输入端口名。</summary>
    internal static PortName GoalPort { get; } = new("目标");

    /// <summary>执行节点承接计划的输入端口名。</summary>
    internal static PortName TaskPort { get; } = new("任务");

    /// <summary>建立内置节点定义集合。Llm 节点使用给定的执行运行器工厂。</summary>
    internal static IReadOnlyList<NodeSpec> Create(IExecutionRunnerFactory llmRunner)
    {
        NodeSpec plan = NodeSpec.Define(new NodeName("规划")) with
        {
            Type = LlmNodeTypes.Create(
                new LlmParameters
                {
                    Prompt = "把目标写成可实施的计划，逐项给出要做什么与验收标准。",
                },
                llmRunner),
            Inputs =
            [
                new InputPort(GoalPort, PortTypes.Text, "要规划的任务目标。"),
                new InputPort(LlmNodeTypes.ModelPort, PortTypes.Text, "执行使用的模型名。", mode: PortMode.Constant),
            ],
            Outputs = [new Port(PlanPort, PortTypes.Text, "目标对应的实施计划。")],
            Spec = new ExecutableSpec(),
        };

        NodeSpec execute = NodeSpec.Define(new NodeName("执行")) with
        {
            Type = LlmNodeTypes.Create(
                new LlmParameters
                {
                    Tools = [new ToolPath("GetLocalTime")],
                },
                llmRunner),
            Inputs =
            [
                new InputPort(TaskPort, PortTypes.Text, "待实施的计划，来自规划节点。"),
                new InputPort(LlmNodeTypes.ModelPort, PortTypes.Text, "执行使用的模型名。", mode: PortMode.Constant),
            ],
            Spec = new ExecutableSpec(),
        };

        NodeSpec delivery = NodeSpec.Define(new NodeName("交付")) with
        {
            Type = ContainerNodeTypes.Type,
            Inputs =
            [
                new InputPort(PlanPort, PortTypes.Text, "待实施的计划，来自规划节点。"),
                new InputPort(
                    LlmNodeTypes.ModelPort,
                    PortTypes.Text,
                    "实施使用的模型名，由宿主在任务启动时给出。",
                    mode: PortMode.Constant),
            ],
            Nodes =
            [
                NodeSpec.Reference(new NodeLabel("实施"), new NodeName("执行")) with
                {
                    Bindings = new Dictionary<PortName, PortRef>
                    {
                        [LlmNodeTypes.ModelPort] = PortRef.Bound(LlmNodeTypes.ModelPort),
                    },
                    From = [new FromGroup([new FromLink(TaskPort, PortRef.Bound(PlanPort))])],
                },
            ],
        };

        return [plan, execute, delivery];
    }
}