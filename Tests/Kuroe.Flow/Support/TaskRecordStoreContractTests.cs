using ErrorOr;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Tasks.Persistence.Files.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.TestSupport;

/// <summary>任务存档存储契约：读写、覆盖、删除、排序与失败隔离。</summary>
public abstract class TaskRecordStoreContractTests
{
    protected abstract ITaskRecordStore CreateStore();

    protected abstract void InjectReadFailure(ITaskRecordStore store, TaskStoreFailure failure);

    [Fact(DisplayName = "读取空存储不返回记录")]
    public void Read_empty_store_returns_no_records()
    {
        TaskStoreReadResult result = CreateStore().ReadAll();

        Assert.Empty(result.Records);
        Assert.Empty(result.Failures);
    }

    [Fact(DisplayName = "写入后读取记录往返")]
    public void Write_then_read_round_trips_record()
    {
        ITaskRecordStore store = CreateStore();
        TaskRecord record = TaskRecordFixture.Create();

        store.Write(record).ThrowIfError();
        TaskStoreReadResult result = store.ReadAll();

        TaskRecord loaded = Assert.Single(result.Records);
        Assert.Equal(record.Identity, loaded.Identity);
        GraphFlowStateRecord state = Assert.IsType<GraphFlowStateRecord>(record.Flow.State);
        GraphFlowStateRecord loadedState = Assert.IsType<GraphFlowStateRecord>(loaded.Flow.State);
        Assert.Equal(record.Flow.Identity.Kind, loaded.Flow.Identity.Kind);
        Assert.Equal(
            Assert.IsType<GraphFlowDefinitionRecord>(record.Flow.Definition),
            Assert.IsType<GraphFlowDefinitionRecord>(loaded.Flow.Definition));
        Assert.Equal(state.Nodes.Count, loadedState.Nodes.Count);
        Assert.Equal(state.Ports.Count, loadedState.Ports.Count);
        Assert.Equal(state.Pending.Steps, loadedState.Pending.Steps);
    }

    [Fact(DisplayName = "写入同一任务替换记录")]
    public void Write_same_task_replaces_record()
    {
        ITaskRecordStore store = CreateStore();
        TaskRecord original = TaskRecordFixture.Create();
        TaskRecord changed = original with
        {
            Identity = original.Identity with { Title = "新标题" },
        };

        store.Write(original).ThrowIfError();
        store.Write(changed).ThrowIfError();

        TaskRecord loaded = Assert.Single(store.ReadAll().Records);
        Assert.Equal("新标题", loaded.Identity.Title);
    }

    [Fact(DisplayName = "删除移除记录")]
    public void Delete_removes_record()
    {
        ITaskRecordStore store = CreateStore();
        TaskRecord record = TaskRecordFixture.Create();
        store.Write(record).ThrowIfError();

        store.Delete(record.Identity.Id).ThrowIfError();

        Assert.Empty(store.ReadAll().Records);
    }

    [Fact(DisplayName = "删除未知任务幂等")]
    public void Delete_unknown_task_is_idempotent()
    {
        ITaskRecordStore store = CreateStore();

        store.Delete(new TaskId(999)).ThrowIfError();

        Assert.Empty(store.ReadAll().Records);
    }

    [Fact(DisplayName = "全部读取按任务顺序返回记录")]
    public void Read_all_returns_records_in_task_order()
    {
        ITaskRecordStore store = CreateStore();
        store.Write(TaskRecordFixture.Create(3)).ThrowIfError();
        store.Write(TaskRecordFixture.Create(1)).ThrowIfError();
        store.Write(TaskRecordFixture.Create(2)).ThrowIfError();

        TaskStoreReadResult result = store.ReadAll();

        Assert.Equal(
            [new TaskId(1), new TaskId(2), new TaskId(3)],
            result.Records.Select(record => record.Identity.Id));
    }

    [Fact(DisplayName = "全部读取报告失败但不丢弃成功项")]
    public void Read_all_reports_failure_without_dropping_successes()
    {
        ITaskRecordStore store = CreateStore();
        store.Write(TaskRecordFixture.Create()).ThrowIfError();
        InjectReadFailure(store, new TaskStoreFailure("broken.json", [Error.Failure(FlowErrorCodes.RecordRead, "文件损坏。")]));

        TaskStoreReadResult result = store.ReadAll();

        Assert.Single(result.Records);
        TaskStoreFailure failure = Assert.Single(result.Failures);
        Assert.Equal("broken.json", failure.Source);
        Assert.Equal(FlowErrorCodes.RecordRead, failure.Errors[0].Code);
    }
}
