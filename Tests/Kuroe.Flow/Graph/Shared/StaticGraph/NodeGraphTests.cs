using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using ExecutableNode = Kuroe.Flow.Graph.Shared.StaticGraph.ExecutableNode;

using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Graph.Shared.Tests.StaticGraph;

/// <summary>编译后的执行视图：节点统一编号，执行节点按名可定位。</summary>
public sealed class NodeGraphTests
{
    [Fact(DisplayName = "节点图按路径定位执行节点")]
    public void NodeGraph_locates_executables_by_path()
    {
        var graph = new NodeGraph(
        [new Port(new PortName("目标"), PortTypes.Text)],
        [
            new ExecutableNode
            {
                Path = NodePath.Of("规划"),
                Type = new StubNodeType(new NodeKind("Executable")),
                Spec = new ExecutableSpec(),
                Inputs = [],
                Outputs = [],
                MaxExecutions = 100,
            },
            new ExecutableNode
            {
                Path = NodePath.Of("实施"),
                Type = new StubNodeType(new NodeKind("Executable")),
                Spec = new ExecutableSpec(),
                Inputs = [],
                Outputs = [],
                MaxExecutions = 100,
            },
        ], []);

        Assert.Equal(2, graph.Count);
        Assert.Equal(2, graph.TotalExecutables);
        Assert.Equal(NodePath.Of("规划"), graph[NodePath.Of("规划")].Path);
        Assert.Equal(NodePath.Of("实施"), graph[NodePath.Of("实施")].Path);
        Assert.Throws<ArgumentException>(() => graph[NodePath.Of("检查")]);
    }

    private sealed class StubNodeType(NodeKind kind) : INodeType
    {
        public NodeKind Kind { get; } = kind;

        public NodeTypeShape Shape { get; } = new();
    }
}
