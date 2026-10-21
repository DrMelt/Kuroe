using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>任务快照的派生属性：任务进度与执行节点顺位。</summary>
public sealed class SnapshotTests
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
            new TaskId(1), "标题", "目标", Flow, Graph, TaskState.Running, 0,
            [.. Graph.ExecutableNodes.Select(executable => new ExecutableSnapshot(executable, []))],
            states, [], [], 0, DateTimeOffset.UtcNow);
    }

    private static readonly ModelDefinition Planner = new() { Name = new ModelRef("规划者") };

    private static readonly FlowDefinition Flow = new(new FlowName("默认"), null, [Planner],
        [new Port(new PortName("目标"), PortType.Text)],
        new NodeSpec
        {
            Label = new NodeLabel("整体"),
            Type = ContainerNodeTypes.Type,
            Nodes =
            [
                new NodeSpec { Label = new NodeLabel("制定计划"), Type = TestNodeTypes.Executable, Model = Planner.Name, Spec = new ExecutableSpec() },
                new NodeSpec { Label = new NodeLabel("实施"), Type = TestNodeTypes.Executable, Model = Planner.Name, Spec = new ExecutableSpec() },
            ],
        });

    private static readonly NodeGraph Graph = new([
        new StartNode
        {
            Path = new NodePath([StartNode.ReservedLabel]),
            Outputs = [new Port(new PortName("目标"), PortType.Text)],
        },
        new ExecutableNode
        {
            Path = NodePath.Of("制定计划"),
            Type = TestNodeTypes.Executable,
            Spec = new ExecutableSpec(),
            Model = Planner,
            Outputs = [],
            MaxExecutions = 100,
        },
        new ExecutableNode
        {
            Path = NodePath.Of("实施"),
            Type = TestNodeTypes.Executable,
            Spec = new ExecutableSpec(),
            Model = Planner,
            Outputs = [],
            MaxExecutions = 100,
        },
    ], []);
}
