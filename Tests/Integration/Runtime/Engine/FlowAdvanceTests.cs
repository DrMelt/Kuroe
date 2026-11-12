using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>图驱动推进：放行、汇拢、重新执行、未收口与取消。测试流程即计划后实施。</summary>
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
    public void Long_start_input_reaches_execution_untruncated()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        string goal = new('甲', 3000);

        TaskId id = environment.Submit(goal);
        environment.Settle(id);

        IExecution plan = environment.Runner.Started.Single(execution => execution.Input.Node.Label.Value == "制定计划");
        InputValue target = plan.Input.Inputs.Single(value => value.Port == new PortName("目标"));

        Assert.Equal(goal, target.Text);
    }

    [Fact]
    public void Long_upstream_output_reaches_downstream_untruncated()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        string plan = new('计', 3000);
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value == "制定计划"
            ? $"{{\"计划\":\"{plan}\"}}"
            : null;

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        IExecution implement = environment.Runner.Started.Single(execution => execution.Input.Node.Label.Value == "分配执行");
        InputValue value = implement.Input.Inputs.Single(input => input.Port == new PortName("计划"));

        Assert.Equal(plan, value.Text);
    }

    [Fact]
    public void Approval_on_plan_parks_until_approved()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewOnPlanFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Wait(id, snapshot =>
            environment.Awaiting(id, PendingKind.Approve).Count == 1
            && snapshot.Executables[0].Executions.Count == 1);
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
    public void Plan_uncollected_then_rerun_reexecutes_plan()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Single(blocked.Executables[0].Executions);

        environment.Runner.SubmitsPorts = true;
        environment.Tasks.Rerun(id).ThrowIfError();

        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);
        Assert.Equal(2, done.Executables[0].Executions.Count);
    }

    [Fact]
    public void Execution_failure_then_rerun_reexecutes_the_node()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.FailsWhen = execution => execution.Input.Node.Label.Value == "分配执行";

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Rerun(id).ThrowIfError();
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

        Assert.Equal(TaskState.Terminated, canceled.State);
        Assert.DoesNotContain(canceled.ExecutableStates, state => state.State == NodeState.Blocked);
        Assert.DoesNotContain(canceled.ExecutableStates, state => state.Block is not null);
        Assert.Single(environment.Registry.ClearSettled());
    }

    [Fact]
    public void Cancel_task_stops_every_execution()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Gate.HoldAll();

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);
        environment.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = environment.Settle(id);
        Assert.Equal(TaskState.Terminated, canceled.State);
        Assert.Equal(0, canceled.LiveExecutions);
        Assert.DoesNotContain(canceled.ExecutableStates, state => state.Block is not null);
    }
    [Fact]
    public void Pause_interrupts_running_execution_and_resume_waits_for_rerun()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Gate.HoldAll();

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);

        environment.Tasks.Pause(id).ThrowIfError();

        TaskSnapshot paused = environment.Wait(id, snapshot =>
            snapshot.State == TaskState.Paused
            && snapshot.LiveExecutions == 0
            && snapshot.Executables.SelectMany(node => node.Executions)
                .Any(execution => execution.State == ExecutionState.Interrupted));
        Assert.Contains(
            paused.Executables.SelectMany(node => node.Executions),
            execution => execution.State == ExecutionState.Interrupted);

        environment.Tasks.Resume(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked).State);

        environment.Runner.Gate.ClearHolds();
        environment.Tasks.Rerun(id).ThrowIfError();

        Assert.Equal(TaskState.Done, environment.Wait(id, snapshot => snapshot.State == TaskState.Done).State);
    }

    [Fact]
    public async Task Pause_while_waiting_rejects_input_until_resume()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewOnPlanFlow);

        TaskId id = environment.Submit("补齐 README");
        await environment.WaitAsync(id, _ => environment.Awaiting(id, PendingKind.Approve).Count == 1);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));

        environment.Tasks.Pause(id).ThrowIfError();
        Assert.Equal(TaskState.Paused, (await environment.WaitAsync(id, snapshot => snapshot.State == TaskState.Paused)).State);

        ErrorOr<Success> rejected = environment.Pendings.Approve(id);
        Assert.True(rejected.IsError);
        Assert.Equal(FlowErrorCodes.TaskPaused, rejected.FirstError.Code);

        environment.Tasks.Resume(id).ThrowIfError();
        environment.Pendings.Approve(id).ThrowIfError();

        Assert.Equal(TaskState.Done, (await environment.WaitAsync(id, snapshot => snapshot.State == TaskState.Done)).State);
    }

    [Fact]
    public void Done_task_cannot_pause_or_terminate()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("补齐 README");
        Assert.Equal(TaskState.Done, environment.Settle(id).State);
        environment.Wait(id, _ => environment.Registry.Find(id).ThrowIfError().Completed);

        ErrorOr<Success> paused = environment.Tasks.Pause(id);
        Assert.True(paused.IsError);
        Assert.Equal(FlowErrorCodes.TaskDoneCannotPause, paused.FirstError.Code);

        ErrorOr<Success> terminated = environment.Tasks.StopTask(id);
        Assert.True(terminated.IsError);
        Assert.Equal(FlowErrorCodes.TaskDoneCannotTerminate, terminated.FirstError.Code);
    }

    [Fact]
    public void Active_task_cannot_resume()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Gate.HoldAll();

        TaskId id = environment.Submit("补齐 README");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);

        ErrorOr<Success> resumed = environment.Tasks.Resume(id);
        Assert.True(resumed.IsError);
        Assert.Equal(FlowErrorCodes.TaskNotPaused, resumed.FirstError.Code);

        environment.Tasks.StopTask(id).ThrowIfError();
    }

    [Fact]
    public async Task Shutdown_keeps_control_state_and_interrupts_executions()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Gate.HoldAll();

        TaskId id = environment.Submit("补齐 README");
        await environment.WaitAsync(id, snapshot => snapshot.LiveExecutions > 0);
        WorkTask task = environment.Registry.Find(id).ThrowIfError();

        await environment.Tasks.ShutdownAsync();

        Assert.Equal(TaskLifecycle.Active, task.Lifecycle);
        Assert.Contains(task.Executions, execution => execution.State == Kuroe.Flow.Execution.ExecutionState.Interrupted);
    }



    [Fact]
    public void Approve_one_approval_keeps_the_other_waiting()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelApprovalFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot parked = environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Approve).Count == 2);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Equal(2, environment.Awaiting(id, PendingKind.Approve).Count);

        FlowStepPath review = FlowStepPath.Of("整体", "审阅甲");
        environment.Pendings.Approve(id, [review]).ThrowIfError();

        // 只放行甲分支的审批节点，乙分支仍停在待批准
        TaskSnapshot partial = environment.Wait(id, _ =>
            environment.Awaiting(id, PendingKind.Approve).Count == 1
            && environment.Runner.Started.Any(execution =>
                execution.Input.Node.Label.Value == "撰写" && execution.State == ExecutionState.Succeeded));
        Assert.Equal(TaskState.Running, partial.State);
        Assert.Equal("整体/审阅乙", Assert.Single(environment.Awaiting(id, PendingKind.Approve)).Value);
        Assert.DoesNotContain(environment.Runner.Started, execution => execution.Input.Node.Label.Value == "排版");
    }

    /// <summary>计划经审批放行后实施。</summary>
    private const string ReviewOnPlanFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
              { "Name": "制定计划", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "审阅", "Kind": "Approval", "Question": "确认计划", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
              { "Name": "分配执行", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Tools": ["GetLocalTime"], "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅@计划" } ] }
                ]
              }
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
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
              { "Name": "制定计划", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "审阅甲", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
              { "Name": "审阅乙", "Kind": "Approval", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
              { "Name": "撰写", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅甲@计划" } ] },
              { "Name": "排版", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅乙@计划" } ] }
                ]
              }
            }
          ]
        }
        """;
}
