using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>可恢复的中断：收口后自动暂停，恢复时从记录继续，重新执行清除恢复状态。</summary>
public sealed class RestoreTests
{
    [Fact]
    public void A_restorable_failure_pauses_the_task_and_resume_restores()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        TaskSnapshot paused = environment.Settle(id);

        Assert.Equal(TaskState.Paused, paused.State);
        ExecutableStateSnapshot blocked = Assert.Single(
            paused.ExecutableStates,
            state => state.State == NodeState.Blocked);
        Assert.True(blocked.CanRestore);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.True(environment.Runner.Starts.Count >= 2);
        Assert.NotNull(environment.Runner.Starts[1]);
        Assert.Equal(environment.Runner.Started[0].Id, environment.Runner.Started[1].Snapshot().ContinuedFrom);
    }

    [Fact]
    public void Rerun_on_a_paused_task_starts_without_restore_state()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Settle(id).State);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Rerun(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.True(environment.Runner.Starts.Count >= 2);
        Assert.Null(environment.Runner.Starts[1]);
        Assert.Null(environment.Runner.Started[1].Snapshot().ContinuedFrom);
    }

    [Fact]
    public void A_failed_restore_can_retry_from_the_source_state()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Paused);

        environment.Runner.RestoreState = null;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Paused);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.True(environment.Runner.Starts.Count >= 3);
        Assert.NotNull(environment.Runner.Starts[2]);
    }

    [Fact]
    public void A_non_restorable_failure_stays_blocked()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.False(Assert.Single(blocked.ExecutableStates, state => state.State == NodeState.Blocked).CanRestore);
        Assert.True(environment.Tasks.Resume(id).IsError);
    }

    [Fact]
    public void Stopping_an_execution_interrupts_it_and_keeps_the_restore_state()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        TaskCompletionSource hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Runner.HoldFor = _ => hold.Task;

        TaskId id = environment.Submit("目标");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);
        ExecutionId execution = environment.Registry.LiveExecutions().Single().Id;

        environment.Tasks.StopExecution(execution).ThrowIfError();
        TaskSnapshot paused = environment.Wait(id, snapshot => snapshot.State == TaskState.Paused);

        var task = environment.Registry.Find(id).ThrowIfError();
        ExecutionSnapshot stopped = task.Executions.Single(candidate => candidate.Id == execution).Snapshot();
        Assert.Equal(ExecutionState.Interrupted, stopped.State);
        Assert.True(stopped.HasRestoreState);
        Assert.True(Assert.Single(paused.ExecutableStates, state => state.State == NodeState.Blocked).CanRestore);

        environment.Runner.HoldFor = null;
        environment.Runner.RestoreState = null;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        hold.TrySetResult();
    }

    [Fact]
    public void A_queued_execution_restarts_after_pause_and_resume()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ManyRootsFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        TaskCompletionSource hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Runner.HoldFor = _ => hold.Task;

        TaskId id = environment.Submit("目标");
        TaskSnapshot running = environment.Wait(id, snapshot =>
            snapshot.Executables.Sum(node => node.Executions.Count) == 5);
        Assert.Equal(5, running.LiveExecutions);

        environment.Tasks.Pause(id).ThrowIfError();
        environment.Runner.HoldFor = null;
        environment.Wait(id, snapshot => snapshot.State == TaskState.Paused && snapshot.LiveExecutions == 0);
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.DoesNotContain(done.ExecutableStates, state => state.State == NodeState.Blocked);
        Assert.All(done.Executables, node => Assert.Single(node.Executions, execution => execution.State == ExecutionState.Succeeded));

        ExecutableSnapshot queued = done.Executables.Single(node => node.Executable.Label.Value == "五");
        Assert.Equal(1, queued.ExecutionCount);
        Assert.Equal(2, queued.Executions.Count);
        hold.TrySetResult();
    }

    [Fact]
    public void A_failed_restore_that_consumed_requests_does_not_fall_back()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Wait(id, snapshot => snapshot.State == TaskState.Paused).State);

        environment.Runner.RestoreState = null;
        environment.Runner.Usage = new ExecutionUsage(1, 0, TokenUsage.None);
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot blocked = environment.Wait(id, snapshot =>
            snapshot.State == TaskState.Blocked
            && snapshot.LiveExecutions == 0
            && snapshot.Executables.Sum(node => node.Executions.Count) == 2);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(2, task.Executions.Count);
        ExecutionSnapshot second = task.Executions[^1].Snapshot();
        Assert.NotNull(second.ContinuedFrom);
        Assert.Equal(1, environment.Runner.Usage.RequestCount);
        Assert.Equal(1, second.Usage.RequestCount);
        Assert.False(second.HasRestoreState);
        Assert.False(Assert.Single(blocked.ExecutableStates, state => state.State == NodeState.Blocked).CanRestore);
        Assert.Equal(2, environment.Runner.Starts.Count);
        Assert.True(environment.Tasks.Resume(id).IsError);
    }

    [Fact]
    public void A_pending_target_survives_resume_alongside_a_restore()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ParallelRootFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = execution => execution.Input.Node.Label.Value == "先失败";
        environment.Runner.DelayFor = execution => execution.Input.Node.Label.Value == "先失败" ? 1 : 300;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Wait(id, snapshot => snapshot.State == TaskState.Paused).State);
        environment.Wait(id, snapshot => snapshot.ExecutableStates.Any(
            state => state.Node.Label.Value == "后完成" && state.State == NodeState.Done));

        environment.Runner.FailsWhen = null;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Contains(environment.Runner.Started, execution => execution.Input.Node.Label.Value == "下游");
    }

    [Fact]
    public void Rerun_without_a_blocked_node_keeps_the_task_paused()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.InputFlow);
        TaskId id = environment.Submit("目标");
        environment.Settle(id);
        environment.Tasks.Pause(id).ThrowIfError();

        Assert.True(environment.Tasks.Rerun(id).IsError);
        Assert.Equal(TaskState.Paused, environment.Snapshot(id).State);
    }

    [Fact]
    public void Terminating_a_running_restore_leaves_no_state()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Settle(id).State);

        environment.Runner.FailsWhen = null;
        TaskCompletionSource hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Runner.HoldFor = _ => hold.Task;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);

        environment.Tasks.StopTask(id).ThrowIfError();
        TaskSnapshot terminated = environment.Wait(id, snapshot => snapshot.State == TaskState.Terminated);

        var task = environment.Registry.Find(id).ThrowIfError();
        Assert.DoesNotContain(task.Executions, execution => execution.Snapshot().HasRestoreState);
        Assert.DoesNotContain(terminated.ExecutableStates, state => state.State == NodeState.Blocked);
        Assert.True(environment.Tasks.Resume(id).IsError);

        hold.TrySetResult();
    }

    [Fact]
    public void Restore_preparation_runs_once_for_snapshots_and_dispatch()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Settle(id).State);
        Assert.Equal(1, environment.Runner.PrepareCount);

        for (int i = 0; i < 3; i++)
        {
            Assert.True(Assert.Single(
                environment.Snapshot(id).ExecutableStates,
                state => state.State == NodeState.Blocked).CanRestore);
        }

        Assert.Equal(1, environment.Runner.PrepareCount);

        environment.Runner.FailsWhen = null;
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(1, environment.Runner.PrepareCount);
    }

    [Fact]
    public void Restore_inherits_submitted_ports()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("目标");
        Assert.Equal(TaskState.Paused, environment.Settle(id).State);

        environment.Runner.FailsWhen = null;
        environment.Runner.SubmitsPorts = false;
        environment.Tasks.Resume(id).ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.True(task.Executions.Count >= 2);
        Assert.Equal(task.Executions[0].Id, task.Executions[1].Snapshot().ContinuedFrom);
    }

    [Fact]
    public void Resume_during_a_held_execution_does_not_pause_again()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.RestoreState = new ExecutionRestoreState("test", "state");
        environment.Runner.FailsWhen = _ => true;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        environment.Runner.HoldFor = _ => release.Task;
        environment.Runner.IgnoreCancellationWhileHeld = true;

        TaskId id = environment.Submit("目标");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);
        ExecutionId first = environment.Registry.LiveExecutions().Single().Id;

        environment.Tasks.Pause(id).ThrowIfError();
        environment.Tasks.Resume(id).ThrowIfError();
        environment.Runner.HoldFor = null;
        environment.Runner.IgnoreCancellationWhileHeld = false;
        environment.Runner.FailsWhen = execution => execution.Id == first;

        release.TrySetResult();
        TaskSnapshot done = environment.Wait(id, snapshot => snapshot.State == TaskState.Done);

        Assert.Equal(TaskState.Done, done.State);
        Assert.True(environment.Registry.Find(id).ThrowIfError().Executions.Count >= 2);
    }
}