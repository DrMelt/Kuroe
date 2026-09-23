using Kuroe.Agent;
using Kuroe.Workflows;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>并发额度：Agent:MaxConcurrentRuns 限制同时在跑的 agent 数。</summary>
public sealed class ConcurrencyTests
{
    [Fact]
    public void MaxConcurrentRuns_limits_parallel_agents()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Settings.Set("Agent:MaxConcurrentRuns", "1").ThrowIfError();
        harness.Executor.DelayMs = 60;

        harness.Settle(harness.Submit("补齐 README"));

        Assert.Equal(1, harness.Executor.Peak);
    }

    [Fact]
    public void Items_run_in_parallel_within_the_limit()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.DelayMs = 120;

        harness.Settle(harness.Submit("补齐 README"));

        Assert.True(harness.Executor.Peak > 1, $"两个条目本可并行，实测峰值 {harness.Executor.Peak}。");
    }

    [Fact]
    public void Several_tasks_run_concurrently_while_being_read()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.DelayMs = 30;

        TaskId[] ids = [harness.Submit("任务甲"), harness.Submit("任务乙"), harness.Submit("任务丙")];

        // 边推进边读快照，正是宿主浏览时的形态
        foreach (TaskId id in ids)
        {
            TaskSnapshot done = harness.Settle(id);

            Assert.Equal(TaskState.Done, done.State);
            Assert.Equal(5, done.Steps.Sum(step => step.Runs.Count));
        }

        Assert.Equal(3, harness.Registry.ClearFinished());
    }
}
