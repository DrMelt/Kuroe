using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;
using Xunit;

namespace Kuroe.Flow.Tests;

using Flow = Shared.StaticGraph;

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

    private static ExecutableStateSnapshot State(NodePath node, NodeState state) =>
        new(node, state, [], 0, []);

    private static class TaskBuilder
    {
        public static TaskSnapshot Snap(IReadOnlyList<ExecutableStateSnapshot> states) => new(
            new TaskId(1), "标题", "目标", Flow, Graph, TaskState.Running, 0, new Dictionary<NodePath, WorkItemList>(),
            [.. Graph.ExecutableNodes.Select(executable => new ExecutableSnapshot(executable, []))],
            states, [], [], 0, DateTimeOffset.UtcNow);
    }

    private static readonly Flow.ModelDefinition Planner = new() { Name = new ModelRef("规划者") };

    private static readonly FlowDefinition Flow = new(new FlowName("默认"), null, [Planner],
        [new Port(new PortName("目标"), PortType.Text)],
        new NodeSpec
        {
            Name = new NodeName("整体"),
            Type = ContainerNodeTypes.Type,
            Nodes =
            [
                new NodeSpec { Name = new NodeName("制定计划"), Type = TestNodeTypes.Executable, Model = Planner.Name, Execution = new ExecutableSpec() },
                new NodeSpec { Name = new NodeName("实施"), Type = TestNodeTypes.Executable, Model = Planner.Name, Execution = new ExecutableSpec() },
            ],
        });

    private static readonly NodeGraph Graph = new([
        new StartNode
        {
            Path = new NodePath([StartNode.ReservedName]),
            Outputs = [new Port(new PortName("目标"), PortType.Text)],
        },
        new ExecutableNode
        {
            Path = NodePath.Of("制定计划"),
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
}
