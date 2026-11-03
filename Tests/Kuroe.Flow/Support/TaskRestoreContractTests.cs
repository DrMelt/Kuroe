using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Xunit;

namespace Kuroe.Flow.TestSupport;

/// <summary>任务恢复契约：完成、未完成、执行中断与历史恢复。</summary>
public abstract class TaskRestoreContractTests
{
    protected abstract ITaskRestorer CreateRestorer();

    protected abstract CompiledFlow CompileFixtureFlow();

    protected abstract TaskRecord DoneRecord { get; }

    protected abstract TaskRecord UnfinishedRecord { get; }

    protected abstract TaskRecord InterruptedRecord { get; }

    [Fact]
    public void Restore_done_record_keeps_done_state()
    {
        WorkTask task = Restore(DoneRecord);

        Assert.Equal(TaskState.Done, task.State);
    }

    [Fact]
    public void Restore_unfinished_record_enters_paused()
    {
        WorkTask task = Restore(UnfinishedRecord);

        Assert.Equal(TaskState.Paused, task.State);
    }

    [Fact]
    public void Restore_preserves_execution_history()
    {
        WorkTask task = Restore(UnfinishedRecord);

        Assert.Equal(UnfinishedRecord.Executions.Count, task.Executions.Count);
    }

    [Fact]
    public void Restore_interrupted_execution_blocks_node()
    {
        WorkTask task = Restore(InterruptedRecord);

        TaskSnapshot snapshot = task.Snapshot();
        Assert.Contains(
            task.Executions,
            execution => execution.State == ExecutionState.Interrupted);
        Assert.Contains(snapshot.ExecutableStates, state => state.State == NodeState.Blocked);
    }

    private WorkTask Restore(TaskRecord record) =>
        CreateRestorer().Restore(record, CompileFixtureFlow()).ThrowIfError();
}
