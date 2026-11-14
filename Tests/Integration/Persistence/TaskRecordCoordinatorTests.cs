using ErrorOr;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Flow.Node.Shared.Graph.Records;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Saving;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Tasks.Shared;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>存档协调：变更标记、自动写盘、刷新、删除与失败重试。</summary>
public sealed class TaskRecordCoordinatorTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(persistTasks: true);
    private readonly RecordingTaskRecordStore _store = new();

    [Fact]
    public async Task Flush_writes_dirty_task()
    {
        TaskId id = _environment.Submit("写存档");
        TaskRecordCoordinator coordinator = CreateCoordinator();

        coordinator.MarkDirty(id);
        await coordinator.FlushAsync();

        TaskRecord record = Assert.Single(_store.ReadAll().Records);
        Assert.Equal(id, record.Identity.Id);
    }

    [Fact]
    public async Task Flush_single_task_writes_it()
    {
        TaskId id = _environment.Submit("写单个任务");
        TaskRecordCoordinator coordinator = CreateCoordinator();

        await coordinator.FlushAsync(id);

        Assert.Equal(id, Assert.Single(_store.ReadAll().Records).Identity.Id);
    }

    [Fact]
    public async Task Change_writes_without_explicit_flush()
    {
        _ = CreateCoordinator();
        TaskId id = _environment.Submit("自动写盘");

        await _store.NextWrite(record => record.Identity.Id == id);

        Assert.Equal(id, Assert.Single(_store.ReadAll().Records).Identity.Id);
    }

    [Fact]
    public async Task Blocked_task_writes_reason_without_explicit_flush()
    {
        _ = CreateCoordinator();
        _environment.Runner.SubmitsPorts = false;
        TaskId id = _environment.Submit("阻塞写盘");
        await _environment.WaitAsync(id, snapshot => snapshot.State == TaskState.Blocked);

        TaskRecordWrite write = await _store.NextWrite(saved =>
            saved.Identity.Id == id
            && State(saved).Nodes.OfType<ExecutableNodeRecord>().Any(node => node.Block is not null));

        ExecutableNodeRecord blocked = Assert.Single(
            State(write.Record).Nodes.OfType<ExecutableNodeRecord>(),
            node => node.Block is not null);
        NodeBlock block = Assert.IsType<NodeBlock>(blocked.Block);
        Assert.Contains("未交回", block.Summary);
    }

    [Fact]
    public async Task Flush_without_mark_writes_nothing()
    {
        TaskRecordCoordinator coordinator = CreateCoordinator();

        await coordinator.FlushAsync();

        Assert.Empty(_store.ReadAll().Records);
    }

    [Fact]
    public void Remove_deletes_record()
    {
        TaskRecord record = TaskRecordFixture.Create();
        Assert.False(_store.Write(record).IsError);
        TaskRecordCoordinator coordinator = CreateCoordinator();

        coordinator.Remove(record.Identity.Id);

        Assert.Empty(_store.ReadAll().Records);
    }

    [Fact]
    public async Task Completed_task_writes_terminal_record()
    {
        _ = CreateCoordinator();
        TaskId id = _environment.Submit("完成写盘");

        await _environment.SettleAsync(id);
        TaskRecordWrite write = await _store.NextWrite(record => record.Identity.Id == id && record.Completed);

        Assert.True(write.Record.Completed);
    }

    [Fact]
    public async Task Write_failure_keeps_record_pending_until_next_flush()
    {
        TaskId id = _environment.Submit("失败重试");
        InMemoryTaskRecordStore inner = new();
        FailOnceStore store = new(inner);
        TaskRecordCoordinator coordinator = new(new TaskRecordSource(_environment.Registry), store);

        coordinator.MarkDirty(id);
        await store.Failed;

        await coordinator.FlushAsync();

        Assert.Equal(id, Assert.Single(inner.ReadAll().Records).Identity.Id);
    }

    [Fact]
    public async Task Change_during_write_is_written_in_the_same_flush()
    {
        TaskId id = _environment.Submit("写入期间变化");
        InMemoryTaskRecordStore inner = new();
        BlockingStore store = new(inner);
        TaskRecordCoordinator coordinator = new(new TaskRecordSource(_environment.Registry), store);

        coordinator.MarkDirty(id);
        await store.Entered;

        Assert.False(_environment.Tasks.Rename(id, "写入期间改名").IsError);
        store.Release();

        await coordinator.FlushAsync();

        TaskRecord record = Assert.Single(inner.ReadAll().Records);
        Assert.Equal("写入期间改名", record.Identity.Title);
    }

    private TaskRecordCoordinator CreateCoordinator() =>
        new(new TaskRecordSource(_environment.Registry), _store);

    private sealed class FailOnceStore(ITaskRecordStore inner) : ITaskRecordStore
    {
        private readonly TaskCompletionSource _failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _pending = 1;

        /// <summary>第一次写入已尝试并失败。</summary>
        public Task Failed => _failed.Task;

        public TaskStoreReadResult ReadAll() => inner.ReadAll();

        public ErrorOr<Success> Write(TaskRecord record)
        {
            if (Interlocked.Exchange(ref _pending, 0) == 1)
            {
                _failed.TrySetResult();

                return [Error.Failure("Test.Write", "写入失败。")];
            }

            return inner.Write(record);
        }

        public ErrorOr<Success> Delete(TaskId id) => inner.Delete(id);
    }

    private sealed class BlockingStore(ITaskRecordStore inner) : ITaskRecordStore
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _first = 1;

        /// <summary>第一次写入已进入并在此保持。</summary>
        public Task Entered => _entered.Task;

        /// <summary>放行第一次写入。</summary>
        public void Release() => _release.TrySetResult();

        public TaskStoreReadResult ReadAll() => inner.ReadAll();

        public ErrorOr<Success> Write(TaskRecord record)
        {
            if (Interlocked.Exchange(ref _first, 0) == 1)
            {
                _entered.TrySetResult();
                _release.Task.GetAwaiter().GetResult();
            }

            return inner.Write(record);
        }

        public ErrorOr<Success> Delete(TaskId id) => inner.Delete(id);
    }

    private static GraphFlowStateRecord State(TaskRecord record) =>
        Assert.IsType<GraphFlowStateRecord>(record.Flow.State);

    public void Dispose() => _environment.Dispose();
}
