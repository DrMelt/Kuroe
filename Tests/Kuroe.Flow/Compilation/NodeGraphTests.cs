using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.Compilation.Graph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;
using Xunit;

namespace Kuroe.Flow.Compilation.Tests;

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
                Path = new NodePath([StartNode.ReservedLabel]),
                Outputs = [new Port(new PortName("目标"), PortTypes.Text)],
            },
            new ExecutableNode
            {
                Path = NodePath.Of("规划"),
                Type = TestNodeTypes.Executable,
                Spec = new ExecutableSpec(),
                Inputs = [],
                Outputs = [],
                MaxExecutions = 100,
            },
            new ExecutableNode
            {
                Path = NodePath.Of("实施"),
                Type = TestNodeTypes.Executable,
                Spec = new ExecutableSpec(),
                Inputs = [],
                Outputs = [],
                MaxExecutions = 100,
            },
        ], []);

        Assert.Equal(3, graph.Count);
        Assert.Equal(2, graph.TotalExecutables);
        Assert.Equal(NodePath.Of("规划"), graph[NodePath.Of("规划")].Path);
        Assert.Equal(NodePath.Of("实施"), graph[NodePath.Of("实施")].Path);
        Assert.Throws<ArgumentException>(() => graph[NodePath.Of("检查")]);
    }
}
