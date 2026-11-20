using ErrorOr;
using Kuroe.Kernel.Tasks.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Console.Tests.Persistence;

/// <summary>任务持久化端到端契约：暂停、退出、重新加载与恢复推进。</summary>
public abstract class TaskPersistenceAcceptanceTests
{
    protected abstract string CreateRoot();

    protected abstract void DeleteRoot(string root);

    protected abstract Task<ITaskPersistenceSession> CreateSessionAsync(string root);

    [Fact(DisplayName = "提交后暂停并释放重载恢复暂停任务")]
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

    [Fact(DisplayName = "重启加载未完成任务为暂停")]
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

    [Fact(DisplayName = "恢复已加载的等待任务接受回答")]
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
            Assert.False(resumed.IsError, string.Join("；", resumed.ErrorsOrEmptyList.Select(error => error.Description)));

            second.WaitForInput(id);
            ErrorOr<Success> answered = second.Answer(id, "加载后的回答");

            Assert.False(answered.IsError, string.Join("；", answered.ErrorsOrEmptyList.Select(error => error.Description)));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact(DisplayName = "恢复已加载的阻塞任务允许重跑")]
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
            ErrorOr<Success> rerun = second.Rerun(id);

            Assert.False(rerun.IsError, string.Join("；", rerun.ErrorsOrEmptyList.Select(error => error.Description)));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact(DisplayName = "并发会话报告任务被另一会话持有")]
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

    [Fact(DisplayName = "加载的任务保留执行历史")]
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
