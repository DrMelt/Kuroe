using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Pending;
using Kuroe.Flow.Runtime.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>运行时节点按激活懒实例化：只有触达的节点才从图定义创建对象。</summary>
public sealed class LazyRuntimeTests
{
    /// <summary>只有审批节点：提交后应保持运行并等待批准。</summary>
    private const string ApprovalOnlyFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Mode": "Constant", "Value": "目标" }] },
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "基准", "Type": "Text", "Mode": "Constant" }],
                "From": [{ "目标": "Start@目标" }],
                "Bindings": { "基准": "Start@基准" },
                "Nodes": [
                  {
                    "Name": "审阅",
                    "Kind": "Approval",
                    "Question": "确认目标",
                    "Inputs": [{ "Name": "基准", "Type": "Text", "Mode": "Constant" }],
                    "Bindings": { "基准": "@基准" }
                  }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>计划完成后停在审批节点等待批准，实施与下游收尾永不触达。</summary>
    private const string ReviewQueueFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "Start@目标" } ], "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "制定计划", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
            { "Name": "分配执行", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅@计划" } ] },
            { "Name": "收尾", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "分配执行@结论" } ] }
          ] } } ]
        }
        """;

    [Fact]
    public void Submit_returns_running_before_the_approval_node_activates()
    {
        using TestEnvironment environment = TestEnvironment.Create(ApprovalOnlyFlow);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "目标"),
            new FlowName("默认"),
            null);

        Assert.False(submitted.IsError);
        Assert.Equal(TaskState.Running, submitted.Value.State);
    }

    [Fact]
    public void Untouched_nodes_stay_unmaterialized()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewQueueFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot snapshot = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Running, snapshot.State);
        Assert.Null(task.Context.Materialized(task.Graph.PathOf("收尾")));
        Assert.Equal(4, snapshot.ExecutableStates.Count);
    }

    /// <summary>容器成员里尚未放行的成员不因状态查询被实例化：查询只读已实例化节点，未实例化成员视为没有产出。
    /// 库容器内实施的成员在跑时，容器自身与它的下游都还未触达。</summary>
    [Fact]
    public void Unreleased_container_member_stays_unmaterialized()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ContainerOutputsFlow);
        environment.Runner.Gate.Hold(NodePath.Of("整体", "交付", "实施"));

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
        using TestEnvironment environment = TestEnvironment.Create(FlowSamples.ReviewFlow);

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
        environment.Wait(id, snapshot => snapshot.State == TaskState.Terminated);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        NodeRuntime late = task.Context.Runtime(task.Graph.PathOf("实施"));
        Assert.True(late.Canceled);
        Assert.Equal(NodeState.Canceled, environment.Snapshot(id).ExecutableStates[1].State);
        Assert.Empty(environment.Pendings.Pending(id).ThrowIfError());
    }

    /// <summary>已实例化的运行时节点数，未触达的节点不计。</summary>
    private static int MaterializedCount(WorkTask task) =>
        task.Graph.Nodes.Count(node => task.Context.Materialized(node.Path) is not null);
}
