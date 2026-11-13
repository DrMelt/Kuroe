using Kuroe.Flow.Execution;
using Kuroe.Flow.Execution.Records;
using Xunit;

namespace Kuroe.Flow.Execution.Records.Shared.Tests;

/// <summary>中性执行记录的形状与取值。</summary>
public sealed class ExecutionRecordTests
{
    [Fact]
    public void Record_keeps_neutral_execution_ports_and_sources()
    {
        ExecutionPortName port = new("结论");
        ExecutionRecord record = new(
            new ExecutionId(3),
            new ExecutionTargetId("整体/计划"),
            2,
            ExecutionState.Succeeded,
            "完成",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            [],
            new ExecutionUsageRecord(1, 0, new TokenUsageRecord(10, 5, 15, null, null), 10),
            [new ExecutionInputRecord(port, "start:目标", "目标内容")],
            new Dictionary<ExecutionPortName, string> { [port] = "目标内容" },
            new ExecutionRestoreRecord("test", "state"),
            new ExecutionId(2),
            new ExecutionContractRecord(
                new Dictionary<ExecutionPortName, ExecutionOutputValue>
                {
                    [port] = new(port, "完成"),
                }));

        Assert.Equal(new ExecutionTargetId("整体/计划"), record.Target);
        Assert.Equal("start:目标", Assert.Single(record.Inputs).Source);
        Assert.Equal("完成", record.Contract!.Outputs![port].Text);
        Assert.Equal(10, record.Usage.Tokens.Input);
    }

    [Fact]
    public void Record_omits_absent_restore_and_contract()
    {
        ExecutionRecord record = new(
            new ExecutionId(1),
            new ExecutionTargetId("步骤"),
            1,
            ExecutionState.Queued,
            "",
            DateTimeOffset.UnixEpoch,
            null,
            null,
            [],
            new ExecutionUsageRecord(0, 0, new TokenUsageRecord(null, null, null, null, null), null),
            [],
            new Dictionary<ExecutionPortName, string>());

        Assert.Null(record.RestoreData);
        Assert.Null(record.Contract);
        Assert.Null(record.StartedAt);
    }

    [Fact]
    public void Capture_prefers_the_source_key()
    {
        ExecutionSnapshot snapshot = Snapshot(new ExecutionInputValue(
            new ExecutionPortName("输入"),
            "内容",
            "显示来源",
            "execution:1:节点:端口"));

        ExecutionRecord record = snapshot.CaptureRecord();

        Assert.Equal("execution:1:节点:端口", Assert.Single(record.Inputs).Source);
    }

    [Fact]
    public void Capture_falls_back_to_the_display_source()
    {
        ExecutionSnapshot snapshot = Snapshot(new ExecutionInputValue(
            new ExecutionPortName("输入"),
            "内容",
            "显示来源"));

        ExecutionRecord record = snapshot.CaptureRecord();

        Assert.Equal("显示来源", Assert.Single(record.Inputs).Source);
    }

    private static ExecutionSnapshot Snapshot(ExecutionInputValue input) => new(
        new ExecutionId(4),
        new ExecutionRequest(
            new ExecutionOwnerId("task"),
            new ExecutionTargetId("step"),
            "test",
            1,
            [],
            [input],
            new Dictionary<ExecutionPortName, string> { [input.Port] = input.Text },
            []),
        ExecutionState.Queued,
        string.Empty,
        DateTimeOffset.UnixEpoch,
        null,
        null,
        [],
        [],
        ExecutionUsage.None,
        null,
        ExecutionContract.Empty,
        null,
        null);
}