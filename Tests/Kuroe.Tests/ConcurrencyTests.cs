using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>并发额度：Runtime:MaxConcurrentRuns 限制同时在跑的 run 数。</summary>
public sealed class ConcurrencyTests
{
    [Fact]
    public void MaxConcurrentRuns_limits_parallel_runs()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Settings.Set("Runtime:MaxConcurrentRuns", "1").ThrowIfError();
        environment.Executor.DelayMs = 60;

        environment.Settle(environment.Submit("补齐 README"));

        Assert.Equal(1, environment.Executor.Peak);
    }

    [Fact]
    public void Items_run_in_parallel_within_the_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.DelayMs = 120;

        environment.Settle(environment.Submit("补齐 README"));

        Assert.True(environment.Executor.Peak > 1, $"两个条目本可并行，实测峰值 {environment.Executor.Peak}。");
    }

    [Fact]
    public void Several_tasks_run_concurrently_while_being_read()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.DelayMs = 30;

        TaskId[] ids = [environment.Submit("任务甲"), environment.Submit("任务乙"), environment.Submit("任务丙")];

        // 边推进边读快照，正是宿主浏览时的形态
        foreach (TaskId id in ids)
        {
            TaskSnapshot done = environment.Settle(id);

            Assert.Equal(TaskState.Done, done.State);
            Assert.Equal(3, done.Executables.Sum(node => node.Runs.Count));
        }

        Assert.Equal(3, environment.Registry.ClearSettled());
    }
}
