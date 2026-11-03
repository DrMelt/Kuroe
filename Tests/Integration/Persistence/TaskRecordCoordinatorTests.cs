using Kuroe.Flow.Persistence.Saving;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>存档协调：变更标记、刷新、删除与任务走完的终态写盘。</summary>
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
        WaitForDocument(id);

        TaskRecord record = Assert.Single(_store.ReadAll().Records);
        Assert.True(record.Completed);
    }

    private TaskRecordCoordinator CreateCoordinator() =>
        new(new TaskRecordSource(_environment.Registry), _store);

    /// <summary>等终态写盘落到存储，最多十秒。</summary>
    private void WaitForDocument(TaskId id)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (_store.ReadAll().Records.Any(record => record.Identity.Id == id))
            {
                return;
            }

            Thread.Sleep(20);
        }

        Assert.Fail($"任务 {id} 的终态存档没有写入。");
    }

    public void Dispose() => _environment.Dispose();
}
