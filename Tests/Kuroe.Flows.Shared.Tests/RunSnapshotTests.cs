using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.Execution.Runs;
using Kuroe.Flows.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flows.Shared.Tests;

/// <summary>run 快照的派生属性：终态判定与已耗时。</summary>
public sealed class RunSnapshotTests
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
        DateTimeOffset created = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset started = created.AddMinutes(1);
        DateTimeOffset finished = created.AddMinutes(3);

        Assert.Equal(TimeSpan.FromMinutes(2), Run(RunState.Succeeded, created, started, finished).Elapsed);
    }

    [Fact]
    public void Elapsed_falls_back_to_now_while_live()
    {
        DateTimeOffset created = DateTimeOffset.UtcNow.AddSeconds(-10);
        DateTimeOffset started = DateTimeOffset.UtcNow.AddSeconds(-5);

        Assert.InRange(
            Run(RunState.Running, created, started, null).Elapsed,
            TimeSpan.FromSeconds(4.5),
            TimeSpan.FromSeconds(5.5));
        Assert.InRange(
            Run(RunState.Queued, created, null, null).Elapsed,
            TimeSpan.FromSeconds(9.5),
            TimeSpan.FromSeconds(10.5));
    }

    private static RunSnapshot Run(
        RunState state,
        DateTimeOffset? created = null,
        DateTimeOffset? started = null,
        DateTimeOffset? finished = null) =>
        new(new RunId(1), Context(), state, string.Empty,
            created ?? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            started, finished, null, [], [], 0);

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        NodeIndex = 0,
        NodeName = new NodeName("制定计划"),
        Instruction = "做",
    };
}
