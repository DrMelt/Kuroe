using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
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
                await first.PauseAsync(id);
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
    public async Task Restart_does_not_auto_resume()
    {
        string root = CreateRoot();
        try
        {
            TaskId id;
            await using (ITaskPersistenceSession first = await CreateSessionAsync(root))
            {
                id = first.Submit("不自动恢复");
                await first.PauseAsync(id);
                await first.ShutdownAsync();
            }

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);
            await Task.Delay(50);

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
                first.Wait(id, snapshot => snapshot.State == TaskState.Running);
                await first.PauseAsync(id);
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
    public async Task Resume_loaded_blocked_task_allows_rework()
    {
        string root = CreateRoot();
        try
        {
            TaskId id;
            await using (ITaskPersistenceSession first = await CreateSessionAsync(root))
            {
                id = first.Submit("阻塞任务");
                first.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
                await first.PauseAsync(id);
                await first.ShutdownAsync();
            }

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);
            ErrorOr<Success> resumed = second.Resume(id);
            ErrorOr<Success> reworked = second.Rework(id);

            Assert.False(resumed.IsError);
            Assert.False(reworked.IsError);
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
                TaskSnapshot settled = first.Wait(id, snapshot => snapshot.LiveExecutions == 0);
                executions = settled.Executables.Sum(node => node.Executions.Count);
                await first.PauseAsync(id);
                await first.ShutdownAsync();
            }

            await using ITaskPersistenceSession second = await CreateSessionAsync(root);
            TaskSnapshot loaded = second.Snapshot(id);

            Assert.Equal(executions, loaded.Executables.Sum(node => node.Executions.Count));
        }
        finally
        {
            DeleteRoot(root);
        }
    }
}
