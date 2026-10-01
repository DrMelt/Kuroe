using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>执行次数账在节点上，按整节点与条目分开累计，峰值取其中最大值，以派发的 run 记录为核算真相。</summary>
public sealed class ExecutionCountTests
{
    [Fact]
    public void PerItem_partial_rework_counts_item_runs_independently()
    {
        using KuroeHarness harness = KuroeHarness.Create(SplitFlow);
        harness.Executor.FailsWhen = run =>
            run.Context.NodeIndex == 2 && run.Context.ItemIndex == 0 && run.Context.ExecutionCount == 1;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, task.Runtime.Executable(2).ExecutionCount(0));
        Assert.Equal(1, task.Runtime.Executable(2).ExecutionCount(1));
        Assert.Equal(2, task.Runtime.Executable(2).MaxExecutionCount);
    }

    [Fact]
    public void Single_node_rework_increments_max()
    {
        using KuroeHarness harness = KuroeHarness.Create(SingleNodeFlow);
        harness.Executor.FailsWhen = run => run.Context.NodeIndex == 1 && run.Context.ExecutionCount == 1;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, task.Runtime.Executable(1).ExecutionCount(null));
        Assert.Equal(2, task.Runtime.Executable(1).MaxExecutionCount);
    }

    [Fact]
    public void Blocked_upstream_leaves_downstream_on_zero()
    {
        using KuroeHarness harness = KuroeHarness.Create(ChainFlow);
        harness.Executor.FailsWhen = run => run.Context.NodeIndex == 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(1, task.Runtime.Executable(1).ExecutionCount(null));
        Assert.Equal(0, task.Runtime.Executable(2).ExecutionCount(null));
        Assert.Equal(0, task.Runtime.Executable(2).MaxExecutionCount);
    }

    /// <summary>节点账与派发记录全等：派发即记账，节点账不会多于或少于 run 记录。</summary>
    [Fact]
    public void Executions_agree_with_run_records()
    {
        using KuroeHarness harness = KuroeHarness.Create(SplitFlow);
        harness.Executor.FailsWhen = run => run.Context.NodeIndex == 2 && run.Context.ItemIndex == 0 && run.Context.ExecutionCount == 1;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        harness.Tasks.Rework(id, null).ThrowIfError();
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        foreach (var node in task.Runs.GroupBy(run => run.Context.NodeIndex))
        {
            foreach (var item in node.GroupBy(run => run.Context.ItemIndex))
            {
                Assert.Equal(item.Count(), task.Runtime.Executable(node.Key).ExecutionCount(item.Key));
            }

            Assert.Equal(node.GroupBy(run => run.Context.ItemIndex).Max(group => group.Count()),
                task.Runtime.Executable(node.Key).MaxExecutionCount);
        }
    }

    /// <summary>规划后按条目展开实施，实例失败时节点阻塞。</summary>
    private const string SplitFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "计划", "Model": "执行者", "Output": "Plan" },
                    { "Name": "实施", "Model": "执行者", "Mode": "PerItem", "From": [ "计划" ] }
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "干活", "Model": "执行者" }
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
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "上游", "Model": "执行者" },
                    { "Name": "下游", "Model": "执行者", "From": [ "上游" ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}