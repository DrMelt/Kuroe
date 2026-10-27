using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Pending;
using Kuroe.Flow.Runtime.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>运行时节点按激活懒实例化：只有触达的节点才从图定义创建对象。</summary>
public sealed class LazyRuntimeTests
{
    /// <summary>计划完成后停在审批节点等待批准，实施与下游收尾永不触达。</summary>
    private const string ReviewQueueFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }], "In": { "目标": "Start@目标" }, "Nodes": [
              { "Name": "制定计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
              { "Name": "分配执行", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅@计划" } ] },
              { "Name": "收尾", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "分配执行@结论" } ] }
            ] }
          ] } ]
        }
        """;

    [Fact]
    public void Untouched_nodes_stay_unmaterialized()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewQueueFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot snapshot = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Running, snapshot.State);
        Assert.Null(task.Context.Materialized(task.Context.Graph.PathOf("收尾")));
        Assert.Equal(4, snapshot.ExecutableStates.Count);
    }

    /// <summary>容器成员里尚未放行的成员不因状态查询被实例化：查询只读已实例化节点，未实例化成员视为没有产出。
    /// 库容器内实施的成员在跑时，容器自身与它的下游都还未触达。</summary>
    [Fact]
    public void Unreleased_container_member_stays_unmaterialized()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ContainerOutputsFlow);
        environment.Runner.DelayMs = 200;

        TaskId id = environment.Submit("目标");
        environment.Wait(id, snapshot => snapshot.ExecutableStates[1].State == NodeState.Running);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();

        // 已实例化的是开始、根容器、根容器成员「制定计划」、容器「交付」与在跑的容器成员「交付/实施」
        Assert.Equal(5, MaterializedCount(task));
        Assert.Equal(NodeState.Pending, environment.Snapshot(id).ExecutableStates[2].State);
    }

    [Fact]
    public void Snapshot_never_materializes_nodes()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ReviewFlow);

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        int before = MaterializedCount(task);

        task.Snapshot();
        task.Snapshot();

        Assert.Equal(before, MaterializedCount(task));
    }

    [Fact]
    public void Canceled_task_materializes_late_node_canceled()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Answer).Count > 0);

        environment.Tasks.StopTask(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Canceled);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        NodeRuntime late = task.Context.Runtime(task.Context.Graph.PathOf("实施"));
        Assert.True(late.Canceled);
        Assert.Equal(NodeState.Canceled, environment.Snapshot(id).ExecutableStates[1].State);
        Assert.Empty(environment.Pendings.Pending(id).ThrowIfError());
    }

    /// <summary>已实例化的运行时节点数，未触达的节点不计。</summary>
    private static int MaterializedCount(WorkTask task) =>
        task.Context.Graph.Nodes.Count(node => task.Context.Materialized(node.Path) is not null);
}
