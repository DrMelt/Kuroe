using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Workflows.Execution.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>运行时节点按激活懒实例化：只有触达的节点才从图定义创建对象。</summary>
public sealed class LazyRuntimeTests
{
    /// <summary>计划完成后按条目停在待批准，下游收尾永不触达。</summary>
    private const string ReviewQueueFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "制定计划", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "分配执行", "Kind": "Executable", "Model": "执行者", "Mode": "PerItem", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Gate": "Review", "From": [ { "拆分": "制定计划@拆分" } ] },
              { "Name": "收尾", "Kind": "Executable", "Model": "执行者", "From": [ { "结论": "分配执行@结论" } ] }
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
        Assert.Equal(4, task.Runtime.MaterializedCount);
        Assert.Equal(NodeState.Pending, snapshot.ExecutableStates[2].State);
        Assert.Equal(3, snapshot.ExecutableStates.Count);
    }

    /// <summary>容器成员里尚未放行的成员不因状态查询被实例化：查询只读已实例化节点，未实例化成员视为没有产出。
    /// 库容器内按条目实施的成员在跑时，容器自身与它的下游都还未触达。</summary>
    [Fact]
    public void Unreleased_container_member_stays_unmaterialized()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.ContainerOutputsFlow);
        harness.Executor.DelayMs = 200;

        TaskId id = harness.Submit("目标");
        harness.Wait(id, snapshot => snapshot.ExecutableStates[1].State == NodeState.Running);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();

        // 已实例化的是开始、根容器、根容器成员「制定计划」与在跑的容器成员「交付.实施」
        Assert.Equal(4, task.Runtime.MaterializedCount);
        Assert.Equal(NodeState.Pending, harness.Snapshot(id).ExecutableStates[2].State);
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
        RuntimeExecutable late = task.Runtime.Executable(3);
        Assert.True(late.Canceled);
        Assert.Equal(NodeState.Canceled, harness.Snapshot(id).ExecutableStates[1].State);
    }
}
