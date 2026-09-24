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
        // 两个条目各推到不同步骤，进度取最远的一个
        Assert.Equal(3, Task().Snap([Unit(0, 1), Unit(1, 3)]).FrontierSteps);
        Assert.Equal(2, Task().Snap([Unit(0, 1), Unit(1, 3)]).TotalSteps);
    }

    [Fact]
    public void Task_without_units_has_zero_frontier()
    {
        Assert.Equal(0, Task().Snap([]).FrontierSteps);
        Assert.Equal(2, Task().Snap([]).TotalSteps);
    }

    private static RunSnapshot Run(RunState state, DateTimeOffset? started = null, DateTimeOffset? finished = null) =>
        new(new RunId(1), Context(), state, string.Empty, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            started, finished, null, [], [], 0);

    private static TaskBuilder Task() => new();

    private static UnitSnapshot Unit(int itemIndex, int cursor) =>
        new(itemIndex, cursor, UnitState.Working, UnitVerdict.NotChecked, null, 1, []);

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        Role = RunRole.Plan,
        StepIndex = 0,
        StepName = "规划",
        Instruction = "做",
        Model = "fake",
    };

    private sealed class TaskBuilder
    {
        public TaskSnapshot Snap(IReadOnlyList<UnitSnapshot> units) => new(
            new TaskId(1), "标题", "目标", Flow, TaskState.Running, 0, 0, null,
            [new StepSnapshot(0, Spec, []), new StepSnapshot(1, Spec, [])],
            units, [], 0, DateTimeOffset.UtcNow);
    }

    private static readonly Workflow Flow = new("默认", null,
    [
        new StepSpec { Name = "规划", Role = RunRole.Plan },
        new StepSpec { Name = "实施", Role = RunRole.Implement },
    ]);

    private static readonly StepSpec Spec = new() { Name = "规划", Role = RunRole.Plan };
}