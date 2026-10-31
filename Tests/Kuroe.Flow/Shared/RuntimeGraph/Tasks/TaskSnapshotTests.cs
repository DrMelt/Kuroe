using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Tasks;

/// <summary>任务快照的派生属性：任务进度与执行节点顺位。</summary>
public sealed class TaskSnapshotTests
{
    [Fact]
    public void Task_frontier_comes_from_the_furthest_done_leaf()
    {
        Assert.Equal(2, TaskBuilder.Snap([
            State(NodePath.Of("制定计划"), NodeState.Done),
            State(NodePath.Of("实施"), NodeState.Done)]).FrontierNodes);
        Assert.Equal(2, TaskBuilder.Snap([]).TotalExecutableNodes);
    }

    [Fact]
    public void Task_without_done_leaves_has_zero_frontier()
    {
        Assert.Equal(0, TaskBuilder.Snap([State(NodePath.Of("制定计划"), NodeState.Running)]).FrontierNodes);
        Assert.Equal(2, TaskBuilder.Snap([]).TotalExecutableNodes);
    }

    [Fact]
    public void Executable_ordinal_follows_the_compiled_order()
    {
        TaskSnapshot snapshot = TaskBuilder.Snap([]);

        Assert.Equal(1, snapshot.OrdinalOf(NodePath.Of("制定计划")));
        Assert.Equal(2, snapshot.OrdinalOf(NodePath.Of("实施")));
        Assert.Equal(3, snapshot.OrdinalOf(NodePath.Of("Start")));
    }

    private static ExecutableStateSnapshot State(NodePath node, NodeState state) => new(node, state);

    private static class TaskBuilder
    {
        public static TaskSnapshot Snap(IReadOnlyList<ExecutableStateSnapshot> states) => new(
            new TaskId(1), "标题", StartInputs.Single(new PortName("目标"), "目标"),
            PortTexts.Empty, Flow, Graph, TaskState.Running, 0,
            [.. Graph.ExecutableNodes.Select(executable => new ExecutableSnapshot(executable, PortTexts.Empty, PortTexts.Empty, []))],
            states, [], [], 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private static readonly INodeType ContainerType = new TestNodeType(
        new NodeKind("Container"),
        new NodeTypeShape { Organizes = true });

    private static readonly INodeType ExecutableType = new TestNodeType(
        new NodeKind("Executable"),
        new NodeTypeShape { DispatchesExecutions = true });

    private static readonly FlowDefinition Flow = new(new FlowName("默认"), null,
        [new Port(new PortName("目标"), PortTypes.Text)], new Dictionary<PortName, string>(),
        new NodeSpec
        {
            Label = new NodeLabel("整体"),
            Type = ContainerType,
            Nodes =
            [
                new NodeSpec { Label = new NodeLabel("制定计划"), Type = ExecutableType, Spec = new ExecutableSpec() },
                new NodeSpec { Label = new NodeLabel("实施"), Type = ExecutableType, Spec = new ExecutableSpec() },
            ],
        });

    private static readonly FakeNodeGraph Graph = new([
        new ExecutableNode
        {
            Path = NodePath.Of("制定计划"),
            Type = ExecutableType,
            Spec = new ExecutableSpec(),
            Inputs = [],
            Outputs = [],
            MaxExecutions = 100,
        },
        new ExecutableNode
        {
            Path = NodePath.Of("实施"),
            Type = ExecutableType,
            Spec = new ExecutableSpec(),
            Inputs = [],
            Outputs = [],
            MaxExecutions = 100,
        },
    ]);

    private sealed class TestNodeType(NodeKind kind, NodeTypeShape shape) : INodeType
    {
        public NodeKind Kind { get; } = kind;

        public NodeTypeShape Shape { get; } = shape;
    }

    /// <summary>只提供执行节点表与总数的测试图，其余图查询不参与快照派生。</summary>
    private sealed class FakeNodeGraph(IReadOnlyList<ExecutableNode> executables) : INodeGraph
    {
        public IReadOnlyList<GraphNode> Nodes => executables;

        public StartNode Start => throw new NotSupportedException();

        public IReadOnlyList<ExecutableNode> ExecutableNodes => executables;

        public IReadOnlyList<ContainerNode> Containers => [];

        public IReadOnlyList<FlowEdge> Edges => [];

        public IReadOnlyList<StaticDependency> StaticDependencies => [];

        public int Count => executables.Count;

        public int TotalExecutables => executables.Count;

        public GraphNode this[NodePath path] => executables.Single(node => node.Path == path);

        public IReadOnlyList<GraphNode> ChildrenOf(NodePath? container) => container is null ? executables : [];

        public ContainerNode? ContainerOf(NodePath path) => null;

        public IReadOnlyList<FlowEdge> Incoming(NodePath target) => [];

        public IReadOnlyList<FlowEdge> Outgoing(NodePath source) => [];

        public IReadOnlyList<NodePath> StaticDependents(NodePath source) => [];

        public StaticResolution? StaticInputResolution(NodePath node, PortName port) => null;

        public StaticResolution? ConstantResolution(NodePath node, PortName port) => null;

        public IReadOnlyList<NodePath> ExecutablesIn(NodePath source) => [source];

        public NodeInputPlan? PlanOf(NodePath path) => null;

        public NodeTypeShape? ShapeOf(NodePath path) => null;

        public PortType TypeOf(NodePath node, PortName port) => PortTypes.Text;
    }
}
