using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Execution.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>执行次数账按执行单元累计，以启动的 run 记录为核算真相。</summary>
public sealed class ExecutionCountTests
{
    /// <summary>返工只指定一个条目时，被返工的条目照常重跑，其余停在阻塞的条目不被带上。</summary>
    [Fact]
    public void Rework_of_one_item_leaves_blocked_siblings_waiting()
    {
        using TestEnvironment environment = TestEnvironment.Create(SplitFlow);
        environment.Executor.FailsWhen = run => run.Context.NodeIndex == 3 && run.Context.ExecutionCount == 1;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        WorkTask task = environment.Registry.Find(id).ThrowIfError();

        environment.Tasks.Rework(id, 0).ThrowIfError();
        TaskSnapshot still = environment.Wait(id, snapshot =>
            snapshot.State == TaskState.Blocked
            && snapshot.Executables.Single(node => node.Index == 3)
                .Runs.Any(run => run.Context.ItemIndex == 0 && run.State == RunState.Succeeded));

        ExecutableStateSnapshot node = Assert.Single(still.ExecutableStates, state => state.Index == 3);
        Assert.Equal(NodeState.Blocked, node.State);
        Assert.Equal([1], node.BlockedItems);
        Assert.Equal(1, node.CompletedItems);
        Assert.Equal(2, task.Runtime.Executable(3).ExecutionCount(0));
        Assert.Equal(1, task.Runtime.Executable(3).ExecutionCount(1));
    }

    [Fact]
    public void PerItem_partial_rework_counts_item_runs_independently()
    {
        using TestEnvironment environment = TestEnvironment.Create(SplitFlow);
        environment.Executor.FailsWhen = run =>
            run.Context.NodeIndex == 3 && run.Context.ItemIndex == 0 && run.Context.ExecutionCount == 1;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        environment.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, task.Runtime.Executable(3).ExecutionCount(0));
        Assert.Equal(1, task.Runtime.Executable(3).ExecutionCount(1));
    }

    [Fact]
    public void Single_node_rework_increments_max()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleNodeFlow);
        environment.Executor.FailsWhen = run => run.Context.NodeIndex == 2 && run.Context.ExecutionCount == 1;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        environment.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, task.Runtime.Executable(2).ExecutionCount(null));
    }

    [Fact]
    public void Blocked_upstream_leaves_downstream_on_zero()
    {
        using TestEnvironment environment = TestEnvironment.Create(ChainFlow);
        environment.Executor.FailsWhen = run => run.Context.NodeIndex == 2;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(1, task.Runtime.Executable(2).ExecutionCount(null));
        Assert.Equal(0, task.Runtime.Executable(3).ExecutionCount(null));
    }

    /// <summary>节点账与派发记录全等：派发即记账，节点账不会多于或少于 run 记录。</summary>
    [Fact]
    public void Executions_agree_with_run_records()
    {
        using TestEnvironment environment = TestEnvironment.Create(SplitFlow);
        environment.Executor.FailsWhen = run => run.Context.NodeIndex == 3 && run.Context.ItemIndex == 0 && run.Context.ExecutionCount == 1;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        environment.Tasks.Rework(id, null).ThrowIfError();
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        foreach (var node in task.Runs.GroupBy(run => run.Context.NodeIndex))
        {
            foreach (var item in node.GroupBy(run => run.Context.ItemIndex))
            {
                Assert.Equal(item.Count(), task.Runtime.Executable(node.Key).ExecutionCount(item.Key));
            }
        }
    }

    /// <summary>规划后按条目展开实施，实例失败时节点阻塞。</summary>
    private const string SplitFlow = """
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
                    { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "实施", "Kind": "Llm", "Model": "执行者", "Mode": "PerItem", "From": [ { "拆分": "计划@拆分" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>单个执行节点，失败返工后重跑。</summary>
    private const string SingleNodeFlow = """
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
                    { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>上游阻塞时下游不启动，统计保持 0。</summary>
    private const string ChainFlow = """
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
                    { "Name": "上游", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "下游", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "上游@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
