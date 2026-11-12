using Kuroe.Flow.Presentation;
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
    private static readonly DateTimeOffset At = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

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
            PortTexts.Empty, View(states), TaskState.Running, 0,
            [.. Graph.ExecutableNodes.Select(executable => new ExecutableSnapshot(
                Executable(executable),
                PortTexts.Empty,
                PortTexts.Empty,
                [],
                0,
                executable.MaxExecutions))],
            states, [], [], 0, At, At);

        private static FlowExecutableView Executable(ExecutableNode executable) => new(
            executable.Path,
            executable.Label,
            executable.Kind,
            executable.Type,
            executable.Spec,
            executable.Inputs,
            executable.Outputs,
            executable.OutputBindings,
            executable.Incoming,
            executable.MaxExecutions);

        private static FlowView View(IReadOnlyList<ExecutableStateSnapshot> states)
        {
            Dictionary<NodePath, NodeState> stateByPath = states.ToDictionary(state => state.Node, state => state.State);

            return new FlowView(
                Flow.Name.Value,
                "graph",
                Flow.Description,
                [],
                [.. Graph.ExecutableNodes.Select(executable => new FlowStepView(
                    executable.Path.Value,
                    executable.Label.Value,
                    executable.Kind.Value,
                    stateByPath.GetValueOrDefault(executable.Path, NodeState.Pending).ToString(),
                    [],
                    [],
                    [],
                    executable.MaxExecutions)
                {
                    ParentPath = executable.Path.Parent?.Value,
                })],
                []);
        }
    }

    private static readonly INodeType ContainerType = new TestNodeType(
        new NodeKind("Container"),
        new NodeTypeShape { Organizes = true });

    private static readonly INodeType ExecutableType = new TestNodeType(
        new NodeKind("Executable"),
        new NodeTypeShape { DispatchesExecutions = true });

    private static readonly FlowDefinition Flow = new(new FlowName("默认"), null,
        [new Port(new PortName("目标"), PortTypes.Text)],
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

    private static readonly NodeGraph Graph = new(
        [],
        [
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
        ],
        []);

    private sealed class TestNodeType(NodeKind kind, NodeTypeShape shape) : INodeType
    {
        public NodeKind Kind { get; } = kind;

        public NodeTypeShape Shape { get; } = shape;
    }
}
