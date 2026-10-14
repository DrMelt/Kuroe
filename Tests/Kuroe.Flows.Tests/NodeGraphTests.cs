using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.Flows.StaticGraph;
using ExecutableNode = Kuroe.Flows.Shared.StaticGraph.ExecutableNode;
using Xunit;

namespace Kuroe.Flows.Tests;

/// <summary>编译后的执行视图：节点统一编号，执行节点按名可定位。</summary>
public sealed class NodeGraphTests
{
    [Fact]
    public void NodeGraph_locates_executables_by_name()
    {
        var graph = new NodeGraph(
        [
            new StartNode
            {
                Index = 0,
                Name = StartNode.ReservedName,
                Path = new NodePath([StartNode.ReservedName]),
                Gate = NodeGate.Auto,
                Outputs = [new Port(new PortName("目标"), PortType.Text)],
            },
            new ExecutableNode
            {
                Index = 1,
                Name = new NodeName("规划"),
                Path = new NodePath([new NodeName("规划")]),
                Gate = NodeGate.Auto,
                Kind = NodeKind.Executable,
                Execution = new ExecutableSpec { Mode = NodeMode.Single },
                Model = Planner,
                Outputs = [],
                SystemPrompt = [],
                MaxRuns = 100,
            },
            new ExecutableNode
            {
                Index = 2,
                Name = new NodeName("实施"),
                Path = new NodePath([new NodeName("实施")]),
                Gate = NodeGate.Auto,
                Kind = NodeKind.Executable,
                Execution = new ExecutableSpec { Mode = NodeMode.Single },
                Model = Planner,
                Outputs = [],
                SystemPrompt = [],
                MaxRuns = 100,
            },
        ], []);

        Assert.Equal(3, graph.Count);
        Assert.Equal(2, graph.TotalExecutables);
        Assert.Equal(1, graph.IndexOf(new NodeName("规划")));
        Assert.Equal(2, graph.IndexOf(new NodeName("实施")));
        Assert.Null(graph.IndexOf(new NodeName("检查")));
    }

    private static readonly ModelDefinition Planner = new() { Name = new ModelRef("执行者") };
}
