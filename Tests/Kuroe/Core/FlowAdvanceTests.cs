using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Tests;

/// <summary>图驱动推进：放行、汇拢、返工、未收口与取消。测试流程即计划后实施。</summary>
public sealed class FlowAdvanceTests
{
    [Fact]
    public void Empty_flow_library_rejects_submit_by_name()
    {
        using TestEnvironment environment = TestEnvironment.Create(flowsJson: "{}");

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "补齐 README"), new FlowName("默认"), null);

        Assert.True(submitted.IsError);
        Assert.Contains(submitted.ErrorsOrEmptyList,
            error => error.Description.Contains("没有名为 默认 的流程"));
    }

    [Fact]
    public void Plan_implement_completes_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[0].Executions);
        Assert.Single(done.Executables[1].Executions);
        Assert.Equal(2, done.FrontierNodes);
    }

    [Fact]
    public void Approval_on_plan_parks_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewOnPlanFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
        Assert.Single(parked.Executables[0].Executions);
        Assert.Empty(parked.Executables[2].Executions);

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[2].Executions);
    }

    [Fact]
    public void Plan_step_without_submission_blocks_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        ExecutionSnapshot plan = Assert.Single(blocked.Executables[0].Executions);
        Assert.Equal(ExecutionState.Failed, plan.State);
        Assert.Contains(plan.Failures, failure => failure.Contains("未收口"));
    }

    [Fact]
    public void Plan_uncollected_then_rework_reruns_plan()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Single(blocked.Executables[0].Executions);

        environment.Runner.SubmitsPorts = true;
        environment.Tasks.Rework(id).ThrowIfError();

        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);
        Assert.Equal(2, done.Executables[0].Executions.Count);
    }

    [Fact]
    public void Execution_failure_then_rework_reruns_the_node()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.FailsWhen = execution => execution.Input.Node.Label.Value == "分配执行";

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables[1].Executions.Count);
    }

    [Fact]
    public void Cancel_after_block_settles_the_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.FailsWhen = execution => execution.Input.Node.Label.Value == "分配执行";

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        environment.Tasks.StopTask(id).ThrowIfError();
        TaskSnapshot canceled = environment.Settle(id);

        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.DoesNotContain(canceled.ExecutableStates, state => state.State == NodeState.Blocked);
        Assert.Equal(1, environment.Registry.ClearSettled());
    }

    [Fact]
    public void Cancel_task_stops_every_execution()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.DelayMs = 300;

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);
        environment.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = environment.Settle(id);
        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.Equal(0, canceled.LiveExecutions);
    }

    [Fact]
    public void Approve_one_approval_keeps_the_other_waiting()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelApprovalFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Settle(id);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Equal(2, environment.Awaiting(id, PendingKind.Approve).Count);

        NodePath review = NodePath.Of("整体", "审阅甲");
        environment.Pendings.Approve(id, [review]).ThrowIfError();

        // 只放行甲分支的审批节点，乙分支仍停在待批准
        TaskSnapshot partial = environment.Settle(id);
        Assert.Equal(TaskState.Running, partial.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
    }

    /// <summary>计划经审批放行后实施。</summary>
    private const string ReviewOnPlanFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Name": "审阅", "Kind": "Approval", "Question": "确认计划", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
                { "Name": "分配执行", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Tools": ["GetLocalTime"], "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>两个审批节点并行放行各自的下游。</summary>
    private const string ParallelApprovalFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Name": "审阅甲", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
                { "Name": "审阅乙", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
                { "Name": "撰写", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅甲@计划" } ] },
                { "Name": "排版", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅乙@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}