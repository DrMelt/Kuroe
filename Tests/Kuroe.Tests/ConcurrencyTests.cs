using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>并发额度：Runtime:MaxConcurrentExecutions 限制同时在跑的执行数。</summary>
public sealed class ConcurrencyTests
{
    [Fact]
    public void MaxConcurrentExecutions_limits_parallel_executions()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Settings.Set("Runtime:MaxConcurrentExecutions", "1").ThrowIfError();
        environment.Runner.DelayMs = 60;

        environment.Settle(environment.Submit("补齐 README"));

        Assert.Equal(1, environment.Runner.Peak);
    }

    [Fact]
    public void Independent_nodes_run_in_parallel_within_the_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelFlow);
        environment.Runner.DelayMs = 120;

        environment.Settle(environment.Submit("补齐 README"));

        Assert.True(environment.Runner.Peak > 1, $"两个并行节点本可并行，实测峰值 {environment.Runner.Peak}。");
    }

    [Fact]
    public void Several_tasks_run_concurrently_while_being_read()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.DelayMs = 30;

        TaskId[] ids = [environment.Submit("任务甲"), environment.Submit("任务乙"), environment.Submit("任务丙")];

        // 边推进边读快照，正是宿主浏览时的形态
        foreach (TaskId id in ids)
        {
            TaskSnapshot done = environment.Settle(id);

            Assert.Equal(TaskState.Done, done.State);
            Assert.Equal(2, done.Executables.Sum(node => node.Executions.Count));
        }

        Assert.Equal(3, environment.Registry.ClearSettled());
    }

    /// <summary>两个并行执行节点同取开始入口，可同时在跑。</summary>
    private const string ParallelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "甲", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "乙", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
