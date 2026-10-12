using ApiHub.Shared.Models;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;
using Flow = Kuroe.Shared.Workflows.Flows;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>快照的派生属性：终态判定、已耗时与任务进度。</summary>
public sealed class SnapshotTests
{
    [Fact]
    public void Run_state_tells_whether_it_settles()
    {
        Assert.False(Run(RunState.Queued).IsSettled);
        Assert.False(Run(RunState.Running).IsSettled);
        Assert.True(Run(RunState.Succeeded).IsSettled);
        Assert.True(Run(RunState.Failed).IsSettled);
        Assert.True(Run(RunState.Canceled).IsSettled);
    }

    [Fact]
    public void Elapsed_uses_finished_when_present()
    {
        DateTimeOffset started = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset finished = started.AddMinutes(2);

        Assert.Equal(TimeSpan.FromMinutes(2), Run(RunState.Succeeded, started, finished).Elapsed);
    }

    [Fact]
    public void Elapsed_falls_back_to_now_while_live()
    {
        DateTimeOffset started = DateTimeOffset.UtcNow.AddSeconds(-5);

        TimeSpan elapsed = Run(RunState.Running, started, null).Elapsed;

        Assert.InRange(elapsed, TimeSpan.FromSeconds(4.5), TimeSpan.FromSeconds(5.5));
    }

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

    private static RunSnapshot Run(RunState state, DateTimeOffset? started = null, DateTimeOffset? finished = null) =>
        new(new RunId(1), Context(), state, string.Empty, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            started, finished, null, [], [], 0);

    private static ExecutableStateSnapshot State(int index, NodeState state) =>
        new(index, state, [], 0, [], null);

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        NodeIndex = 0,
        NodeName = new NodeName("制定计划"),
        Instruction = "做",
        Model = ModelName.Create("fake").Value,
    };

    private static class TaskBuilder
    {
        public static TaskSnapshot Snap(IReadOnlyList<ExecutableStateSnapshot> states) => new(
            new TaskId(1), "标题", "目标", Flow, Graph, TaskState.Running, 0, new Dictionary<int, PlanOutput>(),
            [.. Graph.ExecutableNodes.Select((executable, index) => new ExecutableSnapshot(index, executable, []))],
            states, [], [], 0, DateTimeOffset.UtcNow);
    }

    private static readonly Flow.ModelDefinition Planner = new() { Name = new ModelRef("规划者") };

    private static readonly FlowDefinition Flow = new(new FlowName("默认"), null, [Planner],
        [new Port(new PortName("目标"), PortType.Data)],
        new NodeSpec
        {
            Name = new NodeName("整体"),
            Kind = NodeKind.Container,
            Nodes =
            [
                new NodeSpec { Name = new NodeName("制定计划"), Kind = NodeKind.Executable, Model = Planner.Name, Execution = new ExecutableSpec() },
                new NodeSpec { Name = new NodeName("实施"), Kind = NodeKind.Executable, Model = Planner.Name, Execution = new ExecutableSpec() },
            ],
        });

    private static readonly NodeGraph Graph = new([
        new StartNode
        {
            Index = 0,
            Name = StartNode.ReservedName,
            Path = new NodePath([StartNode.ReservedName]),
            Gate = NodeGate.Auto,
            Outputs = [new Port(new PortName("目标"), PortType.Data)],
        },
        new ExecutableNode
        {
            Index = 1,
            Name = new NodeName("制定计划"),
            Path = new NodePath([new NodeName("制定计划")]),
            Gate = NodeGate.Auto,
            Kind = NodeKind.Executable,
            Execution = new ExecutableSpec { Mode = NodeMode.Single },
            Model = Planner,
            From = [],
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
            From = [new Dependency(1, new PortName("拆分"), new PortName("拆分"), PortType.Data, 0)],
            Outputs = [],
            SystemPrompt = [],
            MaxRuns = 100,
        },
    ], []);
}
