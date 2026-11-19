using Kuroe.Tasks.Runtime;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>可恢复的中断：收口后自动暂停，恢复时从记录继续，重新执行与上游变化只让编排失效，恢复数据作为历史保留。</summary>
public sealed class RestoreTests
{
    [Fact(DisplayName = "可恢复失败暂停任务恢复时继续")]
    public void A_restorable_failure_pauses_the_task_and_resume_restores()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        TaskSnapshot paused = environment.Settle(id);

        Assert.Equal(TaskState.Paused, paused.State);
        FlowStepView blocked = Assert.Single(
            paused.View.Steps,
            step => step.State == FlowItemState.Blocked);
        Assert.True(blocked.CanRestore);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.True(environment.Runner.Starts.Count >= 2, $"启动次数为 {environment.Runner.Starts.Count}。");
        Assert.NotNull(environment.Runner.Starts[1]);
        Assert.Equal(environment.Runner.Started[0].Id, environment.Runner.Started[1].Snapshot().ContinuedFrom);
    }

    [Fact(DisplayName = "暂停任务重跑时不带恢复数据")]
    public void Rerun_on_a_paused_task_starts_without_restore_data()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Settle(id).State);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Rerun(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.True(environment.Runner.Starts.Count >= 2, $"启动次数为 {environment.Runner.Starts.Count}。");
        Assert.Null(environment.Runner.Starts[1]);
        Assert.Null(environment.Runner.Started[1].Snapshot().ContinuedFrom);
        ExecutionSnapshot first = environment.GraphExecution(
            new Kuroe.Execution.Shared.Identity.ExecutionId(environment.Registry.Find(id).ThrowIfError().Executions[0].Id.Value))
            .Snapshot();
        Assert.True(first.HasRestoreData);
    }

    [Fact(DisplayName = "成功执行保留恢复数据")]
    public void A_successful_execution_keeps_restore_data()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        ExecutionSnapshot execution = environment.GraphExecution(
            new Kuroe.Execution.Shared.Identity.ExecutionId(environment.Registry.Find(id).ThrowIfError().Executions[0].Id.Value))
            .Snapshot();
        Assert.Equal(ExecutionState.Succeeded, execution.State);
        Assert.True(execution.HasRestoreData);
    }

    [Fact(DisplayName = "恢复失败可从来源数据重试")]
    public void A_failed_restore_can_retry_from_the_source_data()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Paused);

        environment.Runner.RestoreData = null;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Paused);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.True(environment.Runner.Starts.Count >= 3, $"启动次数为 {environment.Runner.Starts.Count}。");
        Assert.NotNull(environment.Runner.Starts[2]);
    }

    [Fact(DisplayName = "不可恢复的失败保持阻塞")]
    public void A_non_restorable_failure_stays_blocked()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.False(Assert.Single(blocked.View.Steps, step => step.State == FlowItemState.Blocked).CanRestore);
        Assert.True(environment.Tasks.Resume(id).IsError);
    }

    [Fact(DisplayName = "停止执行中断并保留恢复数据")]
    public void Stopping_an_execution_interrupts_it_and_keeps_the_restore_data()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.Gate.HoldAll();

        TaskId id = environment.Submit("目标");
        environment.Wait(id, snapshot => snapshot.Executions
            .Any(entry => entry.State == Kuroe.Execution.Shared.State.ExecutionState.Running));
        Kuroe.Execution.Shared.Identity.ExecutionId execution = environment.Registry.LiveExecutions()
            .Single(entry => entry.State == Kuroe.Execution.Shared.State.ExecutionState.Running).Id;

        environment.Tasks.StopExecution(execution).ThrowIfError();
        TaskSnapshot paused = environment.Wait(id, snapshot =>
            snapshot.State == TaskState.Paused
            && snapshot.Executions
                .All(entry => entry.State == Kuroe.Execution.Shared.State.ExecutionState.Interrupted));

        var task = environment.Registry.Find(id).ThrowIfError();
        Kuroe.Execution.Shared.State.ExecutionSnapshot stopped = task.Executions.Single(candidate => candidate.Id == execution).Snapshot();
        Assert.Equal(Kuroe.Execution.Shared.State.ExecutionState.Interrupted, stopped.State);
        Assert.NotNull(stopped.RestoreData);
        Assert.True(Assert.Single(paused.View.Steps, step => step.State == FlowItemState.Blocked).CanRestore);

        environment.Runner.Gate.ClearHolds();
        environment.Runner.RestoreData = null;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Done);
    }

    [Fact(DisplayName = "排队执行在暂停恢复后重新启动")]
    public void A_queued_execution_restarts_after_pause_and_resume()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ManyRootsFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.Gate.HoldAll();

        TaskId id = environment.Submit("目标");
        TaskSnapshot running = environment.Wait(
            id,
            snapshot => snapshot.View.Steps.Sum(step => step.Executions.Count) == 5);
        Assert.True(
            SpinWait.SpinUntil(() => environment.Runner.Started.Count == 4, TimeSpan.FromSeconds(5)),
            $"已启动执行 {environment.Runner.Started.Count} 个。");
        Assert.Equal(5, running.LiveExecutions);

        environment.Tasks.Pause(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Paused && snapshot.LiveExecutions == 0);
        environment.Runner.Gate.ClearHolds();
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.DoesNotContain(done.View.Steps, step => step.State == FlowItemState.Blocked);
        Assert.All(done.View.Steps, step =>
        {
            Assert.Equal(2, step.ExecutionCount);
            Assert.Equal(2, step.Executions.Count);
            Assert.Single(
                step.Executions,
                execution => execution.State == Kuroe.Execution.Shared.State.ExecutionState.Succeeded);
        });
    }

    [Fact(DisplayName = "已消耗请求的失败恢复不回退")]
    public void A_failed_restore_that_consumed_requests_does_not_fall_back()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Wait(id, snapshot => snapshot.State == TaskState.Paused).State);

        environment.Runner.RestoreData = null;
        environment.Runner.Usage = new Kuroe.Execution.Shared.Usage.ExecutionUsage(
            1,
            0,
            Kuroe.Execution.Shared.Usage.TokenUsage.None);
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot blocked = environment.Wait(id, snapshot =>
            snapshot.State == TaskState.Blocked
            && snapshot.LiveExecutions == 0
            && snapshot.View.Steps.Sum(step => step.Executions.Count) == 2);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(2, task.Executions.Count);
        Kuroe.Execution.Shared.State.ExecutionSnapshot second = task.Executions[^1].Snapshot();
        Assert.NotNull(second.ContinuedFrom);
        Assert.Equal(1, environment.Runner.Usage.RequestCount);
        Assert.Equal(1, second.Usage.RequestCount);
        Assert.Null(second.RestoreData);
        Assert.False(Assert.Single(blocked.View.Steps, step => step.State == FlowItemState.Blocked).CanRestore);
        Assert.Equal(2, environment.Runner.Starts.Count);
        Assert.True(environment.Tasks.Resume(id).IsError);
    }

    [Fact(DisplayName = "待处理目标与恢复并存并在恢复后保留")]
    public void A_pending_target_survives_resume_alongside_a_restore()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ParallelRootFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = execution => execution.Input.Node.Label.Value == "先失败";
        environment.Runner.Gate.Hold(NodePath.Of("后完成"));

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Wait(id, snapshot => snapshot.State == TaskState.Paused).State);
        environment.Runner.Gate.Open(NodePath.Of("后完成"));
        environment.Wait(id, snapshot => snapshot.View.Steps.Any(
            step => step.Label == "后完成" && step.State == FlowItemState.Done));

        environment.Runner.FailsWhen = null;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Contains(environment.Runner.Started, execution => execution.Input.Node.Label.Value == "下游");
    }

    [Fact(DisplayName = "无阻塞节点时重跑保持任务暂停")]
    public void Rerun_without_a_blocked_node_keeps_the_task_paused()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);
        TaskId id = environment.Submit("目标");
        environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Answer).Count == 1);
        environment.Tasks.Pause(id).ThrowIfError();

        Assert.True(environment.Tasks.Rerun(id).IsError);
        Assert.Equal(TaskState.Paused, environment.Snapshot(id).State);
    }

    [Fact(DisplayName = "终止运行中的恢复保留恢复数据")]
    public void Terminating_a_running_restore_keeps_restore_data()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Settle(id).State);

        environment.Runner.FailsWhen = null;
        environment.Runner.Gate.HoldAll();
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);

        environment.Tasks.StopTask(id).ThrowIfError();
        TaskSnapshot terminated = environment.Wait(id, snapshot =>
            snapshot.State == TaskState.Terminated
            && snapshot.Executions
                .All(entry => entry.State != Kuroe.Execution.Shared.State.ExecutionState.Running));

        var task = environment.Registry.Find(id).ThrowIfError();
        Assert.Contains(task.Executions, execution => execution.Snapshot().RestoreData is not null);
        Kuroe.Execution.Shared.State.ExecutionSnapshot terminatedExecution = task.Executions[^1].Snapshot();
        Assert.Equal(Kuroe.Execution.Shared.State.ExecutionState.Interrupted, terminatedExecution.State);
        Assert.NotNull(terminatedExecution.RestoreData);
        Assert.DoesNotContain(terminated.View.Steps, step => step.State == FlowItemState.Blocked);
        Assert.True(environment.Tasks.Resume(id).IsError);

        environment.Runner.Gate.ClearHolds();
    }

    [Fact(DisplayName = "恢复准备对快照与派发只运行一次")]
    public void Restore_preparation_runs_once_for_snapshots_and_dispatch()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Settle(id).State);
        Assert.Equal(1, environment.Runner.PrepareCount);

        for (int i = 0; i < 3; i++)
        {
            Assert.True(Assert.Single(
                environment.Snapshot(id).View.Steps,
                step => step.State == FlowItemState.Blocked).CanRestore);
        }

        Assert.Equal(1, environment.Runner.PrepareCount);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(1, environment.Runner.PrepareCount);
    }

    [Fact(DisplayName = "恢复继承已提交端口")]
    public void Restore_inherits_submitted_ports()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Settle(id).State);

        environment.Runner.FailsWhen = null;
        environment.Runner.SubmitsPorts = false;
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.True(task.Executions.Count >= 2, $"执行次数为 {task.Executions.Count}。");
        Assert.Equal(task.Executions[0].Id, task.Executions[1].Snapshot().ContinuedFrom);
    }

    [Fact(DisplayName = "执行被保持时恢复不再次暂停")]
    public void Resume_during_a_held_execution_does_not_pause_again()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;
        environment.Runner.Gate.HoldAll();
        environment.Runner.IgnoreCancellationWhileHeld = true;

        TaskId id = environment.Submit("目标");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);
        Kuroe.Execution.Shared.Identity.ExecutionId first = environment.Registry.LiveExecutions().Single().Id;

        environment.Tasks.Pause(id).ThrowIfError();
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Runner.IgnoreCancellationWhileHeld = false;
        environment.Runner.FailsWhen = execution => execution.Id.Value == first.Value;

        environment.Runner.Gate.ClearHolds();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.True(environment.Registry.Find(id).ThrowIfError().Executions.Count >= 2, $"执行次数为 {environment.Registry.Find(id).ThrowIfError().Executions.Count}。");
    }
}