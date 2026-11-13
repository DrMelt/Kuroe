using ErrorOr;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Console.Tests.Persistence;

/// <summary>任务持久化端到端契约：暂停、退出、重新加载与恢复推进。</summary>
public abstract class TaskPersistenceAcceptanceTests
{
    protected abstract string CreateRoot();

    protected abstract void DeleteRoot(string root);

    protected abstract Task<ITaskPersistenceSession> CreateSessionAsync(string root);

    [Fact]
    public async Task Submit_pause_dispose_reload_restores_paused_task()
    {
        string root = CreateRoot();
        try
        {
            TaskId id;
            await using (ITaskPersistenceSession first = await CreateSessionAsync(root))
            {
                id = first.Submit("持久化任务");
                first.WaitForInput(id);
                ErrorOr<Success> paused = await first.PauseAsync(id);
                Assert.False(paused.IsError);
                await first.ShutdownAsync();
            }

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);
            Assert.Contains(second.Tasks(), task => task.Id == id);
            Assert.Equal(TaskState.Paused, second.Snapshot(id).State);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Restart_loads_unfinished_task_paused()
    {
        string root = CreateRoot();
        try
        {
            TaskId id;
            await using (ITaskPersistenceSession first = await CreateSessionAsync(root))
            {
                id = first.Submit("不自动恢复");
                first.WaitForInput(id);
                ErrorOr<Success> paused = await first.PauseAsync(id);
                Assert.False(paused.IsError);
                await first.ShutdownAsync();
            }

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);

            TaskSnapshot loaded = second.Snapshot(id);
            Assert.Equal(TaskState.Paused, loaded.State);
            Assert.Equal(0, loaded.LiveExecutions);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Resume_loaded_waiting_task_accepts_answer()
    {
        string root = CreateRoot();
        try
        {
            TaskId id;
            await using (ITaskPersistenceSession first = await CreateSessionAsync(root))
            {
                id = first.Submit("等待输入");
                first.WaitForInput(id);
                ErrorOr<Success> paused = await first.PauseAsync(id);
                Assert.False(paused.IsError);
                await first.ShutdownAsync();
            }

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);
            ErrorOr<Success> resumed = second.Resume(id);
            ErrorOr<Success> answered = second.Answer(id, "加载后的回答");

            Assert.False(resumed.IsError);
            Assert.False(answered.IsError);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Resume_loaded_blocked_task_allows_rerun()
    {
        string root = CreateRoot();
        try
        {
            TaskId id;
            await using (ITaskPersistenceSession first = await CreateSessionAsync(root))
            {
                id = first.Submit("阻塞任务");
                TaskSnapshot blocked = first.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
                Assert.Contains(blocked.View.Steps, step => step.Block is not null);
                await first.PauseAsync(id);
                await first.ShutdownAsync();
            }

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);
            TaskSnapshot loaded = second.Snapshot(id);
            Assert.Contains(loaded.View.Steps, step => step.Block is not null);
            ErrorOr<Success> resumed = second.Resume(id);
            ErrorOr<Success> rerun = second.Rerun(id);

            Assert.False(resumed.IsError);
            Assert.False(rerun.IsError);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Concurrent_sessions_report_the_task_held_by_the_other()
    {
        string root = CreateRoot();
        try
        {
            await using ITaskPersistenceSession first = await CreateSessionAsync(root);
            TaskId held = first.Submit("占用任务");
            first.WaitForInput(held);
            await first.FlushAsync(held);

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);

            Assert.Contains(held, second.Loaded.Busy);
            Assert.DoesNotContain(second.Tasks(), task => task.Id == held);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Loaded_task_preserves_execution_history()
    {
        string root = CreateRoot();
        try
        {
            TaskId id;
            int executions;
            await using (ITaskPersistenceSession first = await CreateSessionAsync(root))
            {
                id = first.Submit("历史任务");
                TaskSnapshot settled = first.WaitForInput(id);
                executions = settled.Executions.Count;
                ErrorOr<Success> paused = await first.PauseAsync(id);
                Assert.False(paused.IsError);
                await first.ShutdownAsync();
            }

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);
            TaskSnapshot loaded = second.Snapshot(id);

            Assert.Equal(executions, loaded.Executions.Count);
        }
        finally
        {
            DeleteRoot(root);
        }
    }
}
