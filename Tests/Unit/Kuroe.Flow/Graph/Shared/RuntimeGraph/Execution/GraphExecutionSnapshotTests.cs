using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using ExecutionUsage = Kuroe.Kernel.Execution.Shared.Usage.ExecutionUsage;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Flow.Graph.Shared.Tests.RuntimeGraph.Execution;

/// <summary>执行快照的派生属性：已耗时。</summary>
public sealed class GraphExecutionSnapshotTests
{
    [Fact(DisplayName = "有完成时间时用其计算耗时")]
    public void Elapsed_uses_finished_when_present()
    {
        DateTimeOffset created = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset started = created.AddMinutes(1);
        DateTimeOffset finished = created.AddMinutes(3);

        Assert.Equal(TimeSpan.FromMinutes(2), Execution(ExecutionState.Succeeded, created, started, finished).Elapsed);
    }

    [Fact(DisplayName = "运行中耗时回退到当前时间")]
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

    private static GraphExecutionSnapshot Execution(
        ExecutionState state,
        DateTimeOffset? created = null,
        DateTimeOffset? started = null,
        DateTimeOffset? finished = null) =>
        new(new ExecutionId(1), Input(), state, string.Empty,
            created ?? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            started, finished, [], [], ExecutionUsage.None);

    private static GraphExecutionInput Input() => new()
    {
        Task = new TaskId(1),
        Node = NodePath.Of("制定计划"),
        Type = new StubType(new NodeKind("Executable")),
    };

    private sealed class StubType(NodeKind kind) : INodeType
    {
        public NodeKind Kind { get; } = kind;

        public NodeTypeShape Shape { get; } = new();
    }
}
