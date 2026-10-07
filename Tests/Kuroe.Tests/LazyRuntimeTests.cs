using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>运行时节点按激活懒物化：只有触达的节点才从图定义创建对象。</summary>
public sealed class LazyRuntimeTests
{
    /// <summary>计划完成后按条目停在待批准，下游收尾永不触达。</summary>
    private const string ReviewQueueFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "制定计划", "Model": "执行者", "Output": "Plan" },
              { "Name": "分配执行", "Model": "执行者", "Mode": "PerItem", "Gate": "Review", "From": ["制定计划"] },
              { "Name": "收尾", "Model": "执行者", "From": ["分配执行"] }
            ] }
          ] } ]
        }
        """;

    [Fact]
    public void Untouched_nodes_stay_unmaterialized()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewQueueFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot snapshot = harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.AwaitingApproval, snapshot.State);
        Assert.Equal(3, task.Runtime.MaterializedCount);
        Assert.Equal(NodeState.Pending, snapshot.ExecutableStates[2].State);
        Assert.Equal(3, snapshot.ExecutableStates.Count);
    }

    [Fact]
    public void Snapshot_never_materializes_nodes()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.ReviewPerItemFlow);

        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        int before = task.Runtime.MaterializedCount;

        task.Snapshot();
        task.Snapshot();

        Assert.Equal(before, task.Runtime.MaterializedCount);
    }

    [Fact]
    public void Canceled_task_materializes_late_node_canceled()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.InputFlow);

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput));

        harness.Tasks.StopTask(id).ThrowIfError();
        harness.Wait(id, snapshot => snapshot.State == TaskState.Canceled);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        RuntimeExecutable late = task.Runtime.Executable(2);
        Assert.True(late.Canceled);
        Assert.Equal(NodeState.Canceled, harness.Snapshot(id).ExecutableStates[1].State);
    }

    [Fact]
    public void Anyof_rejoin_never_double_starts()
    {
        const string flow = """
            { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
              { "Name": "整体", "Nodes": [
                { "Name": "甲", "Model": "执行者" },
                { "Name": "乙", "Model": "执行者" },
                { "Name": "汇合", "Model": "执行者", "AnyOf": [["甲", "乙"], ["乙", "甲"]] }
              ] }
            ] } ] }
            """;

        for (int i = 0; i < 30; i++)
        {
            using KuroeHarness harness = KuroeHarness.Create(flow);
            harness.Executor.DelayMs = 1;

            TaskId id = harness.Submit("目标");
            TaskSnapshot done = harness.Settle(id);

            Assert.Single(done.Executables[2].Runs);
        }
    }
}
