using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>编译后的执行视图：节点统一编号，执行节点按名可定位。</summary>
public sealed class NodeGraphTests
{
    [Fact]
    public void NodeGraph_locates_executables_by_path()
    {
        var graph = new NodeGraph(
        [
            new StartNode
            {
                Path = new NodePath([StartNode.ReservedName]),
                Outputs = [new Port(new PortName("目标"), PortType.Text)],
            },
            new ExecutableNode
            {
                Path = NodePath.Of("规划"),
                Type = TestNodeTypes.Executable,
                Execution = new ExecutableSpec { Mode = NodeMode.Single },
                Model = Planner,
                Outputs = [],
                MaxRuns = 100,
            },
            new ExecutableNode
            {
                Path = NodePath.Of("实施"),
                Type = TestNodeTypes.Executable,
                Execution = new ExecutableSpec { Mode = NodeMode.Single },
                Model = Planner,
                Outputs = [],
                MaxRuns = 100,
            },
        ], []);

        Assert.Equal(3, graph.Count);
        Assert.Equal(2, graph.TotalExecutables);
        Assert.Equal(NodePath.Of("规划"), graph[NodePath.Of("规划")].Path);
        Assert.Equal(NodePath.Of("实施"), graph[NodePath.Of("实施")].Path);
        Assert.Throws<ArgumentException>(() => graph[NodePath.Of("检查")]);
    }

    private static readonly ModelDefinition Planner = new() { Name = new ModelRef("执行者") };
}
