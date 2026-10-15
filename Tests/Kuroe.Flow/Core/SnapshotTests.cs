using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;
using Xunit;

namespace Kuroe.Flow.Tests;

using Flow = Kuroe.Flow.Shared.StaticGraph;

/// <summary>任务快照的派生属性：任务进度与执行节点顺位。</summary>
public sealed class SnapshotTests
{
    [Fact]
    public void Task_frontier_comes_from_the_furthest_done_leaf()
    {
        Assert.Equal(2, TaskBuilder.Snap([
            State(1, NodeState.Done),
            State(2, NodeState.Done)]).FrontierNodes);
        Assert.Equal(2, TaskBuilder.Snap([]).TotalExecutableNodes);
    }

    [Fact]
    public void Task_without_done_leaves_has_zero_frontier()
    {
        Assert.Equal(0, TaskBuilder.Snap([State(1, NodeState.Running)]).FrontierNodes);
        Assert.Equal(2, TaskBuilder.Snap([]).TotalExecutableNodes);
    }

    [Fact]
    public void Executable_ordinal_follows_the_compiled_order()
    {
        TaskSnapshot snapshot = TaskBuilder.Snap([]);

        Assert.Equal(1, snapshot.OrdinalOf(1));
        Assert.Equal(2, snapshot.OrdinalOf(2));
        Assert.Equal(3, snapshot.OrdinalOf(0));
    }

    private static ExecutableStateSnapshot State(int index, NodeState state) =>
        new(index, state, [], 0, []);

    private static class TaskBuilder
    {
        public static TaskSnapshot Snap(IReadOnlyList<ExecutableStateSnapshot> states) => new(
            new TaskId(1), "标题", "目标", Flow, Graph, TaskState.Running, 0, new Dictionary<int, WorkItemList>(),
            [.. Graph.ExecutableNodes.Select((executable, index) => new ExecutableSnapshot(index, executable, []))],
            states, [], [], 0, DateTimeOffset.UtcNow);
    }

    private static readonly Flow.ModelDefinition Planner = new() { Name = new ModelRef("规划者") };

    private static readonly FlowDefinition Flow = new(new FlowName("默认"), null, [Planner],
        [new Port(new PortName("目标"), PortType.Text)],
        new NodeSpec
        {
            Name = new NodeName("整体"),
            Kind = BuiltinNodeKinds.Container,
            Nodes =
            [
                new NodeSpec { Name = new NodeName("制定计划"), Kind = TestNodeTypes.Executable, Model = Planner.Name, Execution = new ExecutableSpec() },
                new NodeSpec { Name = new NodeName("实施"), Kind = TestNodeTypes.Executable, Model = Planner.Name, Execution = new ExecutableSpec() },
            ],
        });

    private static readonly NodeGraph Graph = new([
        new StartNode
        {
            Index = 0,
            Name = StartNode.ReservedName,
            Path = new NodePath([StartNode.ReservedName]),
            Outputs = [new Port(new PortName("目标"), PortType.Text)],
        },
        new ExecutableNode
        {
            Index = 1,
            Name = new NodeName("制定计划"),
            Path = new NodePath([new NodeName("制定计划")]),
            Kind = TestNodeTypes.Executable,
            Execution = new ExecutableSpec { Mode = NodeMode.Single },
            Model = Planner,
            Outputs = [],
            Parameters = null,
            MaxRuns = 100,
        },
        new ExecutableNode
        {
            Index = 2,
            Name = new NodeName("实施"),
            Path = new NodePath([new NodeName("实施")]),
            Kind = TestNodeTypes.Executable,
            Execution = new ExecutableSpec { Mode = NodeMode.Single },
            Model = Planner,
            Outputs = [],
            Parameters = null,
            MaxRuns = 100,
        },
    ], [], TestNodeTypes.Builtin());
}
