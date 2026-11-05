using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Saving;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>存档协调：变更标记、自动写盘、刷新、删除与失败重试。</summary>
public sealed class TaskRecordCoordinatorTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(persistTasks: true);
    private readonly InMemoryTaskRecordStore _store = new();

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
    public void Change_writes_without_explicit_flush()
    {
        _ = CreateCoordinator();
        TaskId id = _environment.Submit("自动写盘");

        WaitForDocument(id);

        Assert.Equal(id, Assert.Single(_store.ReadAll().Records).Identity.Id);
    }

    [Fact]
    public void Blocked_task_writes_reason_without_explicit_flush()
    {
        _ = CreateCoordinator();
        _environment.Runner.SubmitsPorts = false;
        TaskId id = _environment.Submit("阻塞写盘");
        _environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        TaskRecord record = WaitForRecord(
            id,
            saved => saved.Nodes.OfType<ExecutableNodeRecord>().Any(node => node.Block is not null));

        ExecutableNodeRecord blocked = Assert.Single(
            record.Nodes.OfType<ExecutableNodeRecord>(),
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
    public void Completed_task_writes_terminal_record()
    {
        _ = CreateCoordinator();
        TaskId id = _environment.Submit("完成写盘");

        _environment.Settle(id);
        TaskRecord record = WaitForRecord(id, saved => saved.Completed);

        Assert.True(record.Completed);
    }

    [Fact]
    public async Task Write_failure_keeps_record_pending_until_next_flush()
    {
        TaskId id = _environment.Submit("失败重试");
        InMemoryTaskRecordStore inner = new();
        FailOnceStore store = new(inner);
        TaskRecordCoordinator coordinator = new(new TaskRecordSource(_environment.Registry), store);

        coordinator.MarkDirty(id);
        Assert.True(store.Failed.Wait(TimeSpan.FromSeconds(10)), "写盘没有进入存储。");

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
        Assert.True(store.Entered.Wait(TimeSpan.FromSeconds(10)), "写盘没有进入存储。");

        Assert.False(_environment.Tasks.Rename(id, "写入期间改名").IsError);
        store.Release.Set();

        await coordinator.FlushAsync();

        TaskRecord record = Assert.Single(inner.ReadAll().Records);
        Assert.Equal("写入期间改名", record.Identity.Title);
    }

    private TaskRecordCoordinator CreateCoordinator() =>
        new(new TaskRecordSource(_environment.Registry), _store);

    /// <summary>等任务存档落到存储，最多十秒。</summary>
    private void WaitForDocument(TaskId id) => WaitForRecord(id, _ => true);

    /// <summary>等满足条件的存档落到存储，最多十秒。</summary>
    private TaskRecord WaitForRecord(TaskId id, Func<TaskRecord, bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            TaskRecord? record = _store.ReadAll().Records
                .FirstOrDefault(saved => saved.Identity.Id == id && condition(saved));
            if (record is not null)
            {
                return record;
            }

            Thread.Sleep(20);
        }

        throw new TimeoutException($"任务 {id} 的存档没有写入。");
    }

    private sealed class FailOnceStore(ITaskRecordStore inner) : ITaskRecordStore
    {
        private int _pending = 1;

        public ManualResetEventSlim Failed { get; } = new(false);

        public TaskStoreReadResult ReadAll() => inner.ReadAll();

        public ErrorOr<Success> Write(TaskRecord record)
        {
            if (Interlocked.Exchange(ref _pending, 0) == 1)
            {
                Failed.Set();

                return [Error.Failure("Test.Write", "写入失败。")];
            }

            return inner.Write(record);
        }

        public ErrorOr<Success> Delete(TaskId id) => inner.Delete(id);
    }

    private sealed class BlockingStore(ITaskRecordStore inner) : ITaskRecordStore
    {
        private int _first = 1;

        public ManualResetEventSlim Entered { get; } = new(false);

        public ManualResetEventSlim Release { get; } = new(false);

        public TaskStoreReadResult ReadAll() => inner.ReadAll();

        public ErrorOr<Success> Write(TaskRecord record)
        {
            if (Interlocked.Exchange(ref _first, 0) == 1)
            {
                Entered.Set();
                Release.Wait(TimeSpan.FromSeconds(10));
            }

            return inner.Write(record);
        }

        public ErrorOr<Success> Delete(TaskId id) => inner.Delete(id);
    }

    public void Dispose() => _environment.Dispose();
}
