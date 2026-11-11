using ErrorOr;
using Kuroe.Flow.Execution;
using Xunit;

namespace Kuroe.Flow.Execution.Tests;

/// <summary>执行单元的状态机：启动、契约产出、中断与存档恢复。</summary>
public sealed class ExecutionUnitTests
{
    [Fact]
    public void Mark_running_notifies_once_and_records_start()
    {
        ExecutionUnit unit = new(new ExecutionId(1), Request());
        int started = 0;
        unit.Started += () => started++;

        unit.MarkRunning();
        unit.MarkRunning();

        Assert.Equal(ExecutionState.Running, unit.State);
        Assert.Equal(1, started);
        Assert.NotNull(unit.Snapshot().StartedAt);
    }

    [Fact]
    public void Submitted_outputs_survive_success()
    {
        ExecutionUnit unit = new(new ExecutionId(1), Request());
        ExecutionPortName port = new("结论");
        unit.SubmitOutputs(new Dictionary<ExecutionPortName, ExecutionOutputValue>
        {
            [port] = new(port, "结果", false),
        });

        unit.MarkSucceeded(ExecutionOutput.FromPorts([new ExecutionOutputValue(port, "结果", false)]));

        ExecutionUnitSnapshot snapshot = unit.Snapshot();
        Assert.Equal(ExecutionState.Succeeded, snapshot.State);
        Assert.Equal("结果", snapshot.Contract.Outputs![port].Text);
        Assert.Equal(ExecutionOutputKind.Ports, snapshot.Output?.Kind);
    }

    [Fact]
    public void Interrupt_then_settle_records_reason()
    {
        ExecutionUnit unit = new(new ExecutionId(1), Request());
        unit.MarkRunning();

        unit.Interrupt("任务终止。");
        unit.MarkInterrupted();

        ExecutionUnitSnapshot snapshot = unit.Snapshot();
        Assert.Equal(ExecutionState.Interrupted, snapshot.State);
        Assert.Contains("任务终止。", snapshot.Failures);
    }

    [Fact]
    public void Restoring_a_live_state_marks_interrupted()
    {
        ExecutionUnit unit = ExecutionUnit.Restore(
            new ExecutionId(1),
            Request(),
            DateTimeOffset.UnixEpoch,
            ExecutionState.Running,
            "执行中",
            DateTimeOffset.UnixEpoch,
            null,
            [],
            ExecutionUsageSnapshot.None,
            null,
            ExecutionContract.Empty,
            null);

        Assert.Equal(ExecutionState.Interrupted, unit.State);
        Assert.Contains("进程退出前未收口。", unit.Snapshot().Failures);
    }

    private static ExecutionRequest Request() =>
        new(
            new ExecutionOwnerId("任务"),
            new ExecutionTargetId("节点"),
            "测试",
            1,
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            []);
}