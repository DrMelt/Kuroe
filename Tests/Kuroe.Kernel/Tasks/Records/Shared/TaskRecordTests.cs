using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Tasks.Records.Shared.Tests;

/// <summary>任务信封只保存任务身份、过程记录、流程记录与执行记录。</summary>
public sealed class TaskRecordTests
{
    [Fact]
    public void Envelope_keeps_flow_records_and_executions()
    {
        TaskId id = new(7);
        TaskRecord record = new(
            new TaskIdentity(id, "任务", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
            DateTimeOffset.UnixEpoch,
            TaskLifecycle.Paused,
            Completed: false,
            new TaskJournalRecord([], 0),
            new FlowRecord(
                new FlowIdentity(new FlowName("默认"), "test", "test"),
                new Definition(),
                new State()),
            []);

        Assert.Equal(id, record.Identity.Id);
        Assert.Equal("test", record.Flow.Identity.Kind);
        Assert.IsType<Definition>(record.Flow.Definition);
        Assert.IsType<State>(record.Flow.State);
        Assert.Empty(record.Executions);
    }

    private sealed record Definition : IFlowDefinitionRecord;

    private sealed record State : IFlowStateRecord;
}