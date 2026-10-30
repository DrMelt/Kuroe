using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.TestSupport;

/// <summary>任务检查点存储契约：读写、覆盖、删除、排序与失败隔离。</summary>
public abstract class TaskCheckpointStoreContractTests
{
    protected abstract ITaskCheckpointStore CreateStore();

    protected abstract void InjectReadFailure(ITaskCheckpointStore store, TaskStoreFailure failure);

    [Fact]
    public void Read_empty_store_returns_no_documents()
    {
        TaskStoreReadResult result = CreateStore().ReadAll();

        Assert.Empty(result.Documents);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Write_then_read_round_trips_document()
    {
        ITaskCheckpointStore store = CreateStore();
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument();

        store.Write(document).ThrowIfError();
        TaskStoreReadResult result = store.ReadAll();

        TaskCheckpointDocument loaded = Assert.Single(result.Documents);
        Assert.Equal(document.Version, loaded.Version);
        Assert.Equal(document.Task.Identity, loaded.Task.Identity);
        Assert.Equal(document.Task.Flow, loaded.Task.Flow);
    }

    [Fact]
    public void Write_same_task_replaces_document()
    {
        ITaskCheckpointStore store = CreateStore();
        TaskCheckpointDocument original = TaskCheckpointFixture.CreateDocument();
        TaskCheckpointDocument changed = original with
        {
            Task = original.Task with
            {
                Identity = original.Task.Identity with { Title = "新标题" },
            },
        };

        store.Write(original).ThrowIfError();
        store.Write(changed).ThrowIfError();

        TaskCheckpointDocument loaded = Assert.Single(store.ReadAll().Documents);
        Assert.Equal("新标题", loaded.Task.Identity.Title);
    }

    [Fact]
    public void Delete_removes_document()
    {
        ITaskCheckpointStore store = CreateStore();
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument();
        store.Write(document).ThrowIfError();

        store.Delete(document.Task.Identity.Id).ThrowIfError();

        Assert.Empty(store.ReadAll().Documents);
    }

    [Fact]
    public void Delete_unknown_task_is_idempotent()
    {
        ITaskCheckpointStore store = CreateStore();

        store.Delete(new TaskId(999)).ThrowIfError();

        Assert.Empty(store.ReadAll().Documents);
    }

    [Fact]
    public void Read_all_returns_documents_in_task_order()
    {
        ITaskCheckpointStore store = CreateStore();
        store.Write(TaskCheckpointFixture.CreateDocument(3)).ThrowIfError();
        store.Write(TaskCheckpointFixture.CreateDocument(1)).ThrowIfError();
        store.Write(TaskCheckpointFixture.CreateDocument(2)).ThrowIfError();

        TaskStoreReadResult result = store.ReadAll();

        Assert.Equal(
            [new TaskId(1), new TaskId(2), new TaskId(3)],
            result.Documents.Select(document => document.Task.Identity.Id));
    }

    [Fact]
    public void Read_all_reports_failure_without_dropping_successes()
    {
        ITaskCheckpointStore store = CreateStore();
        store.Write(TaskCheckpointFixture.CreateDocument()).ThrowIfError();
        InjectReadFailure(store, new TaskStoreFailure("broken.json", [Error.Failure(FlowErrorCodes.CheckpointRead, "文件损坏。")]));

        TaskStoreReadResult result = store.ReadAll();

        Assert.Single(result.Documents);
        TaskStoreFailure failure = Assert.Single(result.Failures);
        Assert.Equal("broken.json", failure.Source);
        Assert.Equal(FlowErrorCodes.CheckpointRead, failure.Errors[0].Code);
    }
}
