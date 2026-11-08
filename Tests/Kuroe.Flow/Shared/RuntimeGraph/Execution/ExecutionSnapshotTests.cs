using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>执行快照的派生属性：已耗时。</summary>
public sealed class ExecutionSnapshotTests
{
    [Fact]
    public void Elapsed_uses_finished_when_present()
    {
        DateTimeOffset created = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset started = created.AddMinutes(1);
        DateTimeOffset finished = created.AddMinutes(3);

        Assert.Equal(TimeSpan.FromMinutes(2), Execution(ExecutionState.Succeeded, created, started, finished).Elapsed);
    }

    [Fact]
    public void Elapsed_falls_back_to_now_while_live()
    {
        DateTimeOffset created = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset started = created.AddSeconds(5);
        DateTimeOffset now = created.AddSeconds(10);

        Assert.Equal(
            TimeSpan.FromSeconds(5),
            Execution(ExecutionState.Running, created, started, null).ElapsedAt(now));
        Assert.Equal(
            TimeSpan.FromSeconds(10),
            Execution(ExecutionState.Queued, created, null, null).ElapsedAt(now));
    }

    private static ExecutionSnapshot Execution(
        ExecutionState state,
        DateTimeOffset? created = null,
        DateTimeOffset? started = null,
        DateTimeOffset? finished = null) =>
        new(new ExecutionId(1), Input(), state, string.Empty,
            created ?? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            started, finished, [], [], ExecutionUsage.None);

    private static ExecutionInput Input() => new()
    {
        Task = new TaskId(1),
        Node = NodePath.Of("制定计划"),
        Type = new StubNodeType(new NodeKind("Executable")),
    };
}
