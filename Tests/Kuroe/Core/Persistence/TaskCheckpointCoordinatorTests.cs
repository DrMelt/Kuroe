using Kuroe.Flow.Persistence;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Tests.Persistence;

/// <summary>检查点协调：变更标记、刷新、删除与任务走完的终态写盘。</summary>
public sealed class TaskCheckpointCoordinatorTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(persistTasks: true);
    private readonly InMemoryTaskCheckpointStore _store = new();

    [Fact]
    public async Task Flush_writes_dirty_task()
    {
        TaskId id = _environment.Submit("写检查点");
        TaskCheckpointCoordinator coordinator = CreateCoordinator();

        coordinator.MarkDirty(id);
        await coordinator.FlushAsync();

        TaskCheckpointDocument document = Assert.Single(_store.ReadAll().Documents);
        Assert.Equal(id, document.Task.Identity.Id);
    }

    [Fact]
    public async Task Flush_without_mark_writes_nothing()
    {
        TaskCheckpointCoordinator coordinator = CreateCoordinator();

        await coordinator.FlushAsync();

        Assert.Empty(_store.ReadAll().Documents);
    }

    [Fact]
    public void Remove_deletes_checkpoint()
    {
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument();
        Assert.False(_store.Write(document).IsError);
        TaskCheckpointCoordinator coordinator = CreateCoordinator();

        coordinator.Remove(document.Task.Identity.Id);

        Assert.Empty(_store.ReadAll().Documents);
    }

    [Fact]
    public void Completed_task_writes_terminal_checkpoint()
    {
        _ = CreateCoordinator();
        TaskId id = _environment.Submit("完成写盘");

        _environment.Settle(id);
        WaitForDocument(id);

        TaskCheckpointDocument document = Assert.Single(_store.ReadAll().Documents);
        Assert.True(document.Task.Completed);
    }

    private TaskCheckpointCoordinator CreateCoordinator() =>
        new(new TaskCheckpointSource(_environment.Registry), _store);

    /// <summary>等终态写盘落到存储，最多两秒。</summary>
    private void WaitForDocument(TaskId id)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (_store.ReadAll().Documents.Any(document => document.Task.Identity.Id == id))
            {
                return;
            }

            Thread.Sleep(20);
        }

        Assert.Fail($"任务 {id} 的终态检查点没有写入。");
    }

    public void Dispose() => _environment.Dispose();
}