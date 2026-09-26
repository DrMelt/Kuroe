using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Agent.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
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
    public void Task_frontier_comes_from_the_furthest_unit()
    {
        // 两个条目各推到不同叶子，进度取最远的一个
        Assert.Equal(3, Task().Snap([Unit(0, 1), Unit(1, 3)]).FrontierNodes);
        Assert.Equal(2, Task().Snap([Unit(0, 1), Unit(1, 3)]).TotalNodes);
    }

    [Fact]
    public void Task_without_units_has_zero_frontier()
    {
        Assert.Equal(0, Task().Snap([]).FrontierNodes);
        Assert.Equal(2, Task().Snap([]).TotalNodes);
    }

    private static RunSnapshot Run(RunState state, DateTimeOffset? started = null, DateTimeOffset? finished = null) =>
        new(new RunId(1), Context(), state, string.Empty, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            started, finished, null, [], [], 0);

    private static TaskBuilder Task() => new();

    private static UnitSnapshot Unit(int itemIndex, int cursor) =>
        new(itemIndex, null, cursor, UnitState.Working, UnitVerdict.NotChecked, null, 1, []);

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        Output = NodeOutput.Plan,
        NodeIndex = 0,
        NodeName = "制定计划",
        Instruction = "做",
        Model = "fake",
    };

    private sealed class TaskBuilder
    {
        public TaskSnapshot Snap(IReadOnlyList<UnitSnapshot> units) => new(
            new TaskId(1), "标题", "目标", Flow, Graph, TaskState.Running, 0, 0, new Dictionary<int, PlanOutput>(),
            [.. Graph.Leaves.Select((leaf, index) => new NodeSnapshot(index, leaf, []))],
            units, [], 0, DateTimeOffset.UtcNow);
    }

    private static readonly AgentDefinition Agent = new() { Name = "规划者" };

    private static readonly Workflow Flow = new("默认", null, [Agent],
    [
        new AgentNode { Name = "制定计划", Agent = Agent.Name, Output = NodeOutput.Plan },
        new AgentNode { Name = "实施", Agent = Agent.Name },
    ]);

    private static readonly NodeGraph Graph = new([
        new LeafNode(0, "制定计划", "制定计划", Agent, null, NodeOutput.Plan, NodeMode.Single, [], NodeGate.Auto, null, null, null),
        new LeafNode(1, "实施", "实施", Agent, null, NodeOutput.Plain, NodeMode.Single, [0], NodeGate.Auto, null, null, null),
    ], []);
}