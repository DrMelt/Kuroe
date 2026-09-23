using Kuroe.Agent;
using Kuroe.Agent.Runs;
using Kuroe.Workflows;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>步骤链推进：展开、放行、返工、未收口与取消。</summary>
public sealed class FlowAdvanceTests
{
    [Fact]
    public void Plan_implement_check_completes_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Steps[0].Runs);
        Assert.Equal(2, done.Steps[1].Runs.Count);
        Assert.Equal(2, done.Steps[2].Runs.Count);
        Assert.Equal(3, done.FrontierSteps);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));
    }

    [Fact]
    public void Rejected_check_retries_implement_with_findings()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = run => run.Context.Attempt > 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        // 两个条目各失败一轮再返工，实施与检查各两轮
        Assert.Equal(4, done.Steps[1].Runs.Count);
        Assert.Equal(4, done.Steps[2].Runs.Count);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null), unit =>
        {
            Assert.Equal(2, unit.Attempts);
            Assert.Equal(UnitVerdict.Verified, unit.Verdict);
        });

        RunSnapshot retry = Assert.Single(done.Steps[1].Runs,
            run => run.Context.ItemIndex == 0 && run.Context.Attempt == 2);
        Assert.Contains(retry.Context.Seed, message => message.Source.Label.Contains("检查"));
    }

    [Fact]
    public void Review_gate_parks_unit_until_approved()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewOnPlanFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Single(parked.Steps[0].Runs);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Steps[1].Runs.Count);
    }

    [Fact]
    public void Plan_step_without_submission_blocks_unit()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPlan = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        RunSnapshot plan = Assert.Single(blocked.Steps[0].Runs);
        Assert.Equal(RunState.Failed, plan.State);
        Assert.Contains(plan.Failures, failure => failure.Contains("未收口"));
    }

    [Fact]
    public void Stop_reject_action_parks_unit_until_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create(StopOnRejectFlow);
        harness.Executor.CheckPasses = _ => false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        harness.Tasks.Rework(id, 0).ThrowIfError();
        TaskSnapshot reopened = harness.Wait(id, snapshot =>
            snapshot.Units.Any(unit => unit.ItemIndex == 0 && unit.Attempts > 1));

        Assert.Equal(2, Assert.Single(reopened.Units, unit => unit.ItemIndex == 0).Attempts);
    }

    [Fact]
    public void Cancel_after_fan_out_settles_the_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = run => run.Context.ItemIndex == 0;
        harness.Executor.DelayMs = 30;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        harness.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = harness.Settle(id);

        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.DoesNotContain(canceled.Units, unit => unit.State is UnitState.Blocked or UnitState.AwaitingApproval);
        Assert.Equal(1, harness.Registry.ClearFinished());
    }

    [Fact]
    public void Cancel_task_stops_every_unit()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.DelayMs = 300;

        TaskId id = harness.Submit("补齐 README");
        harness.Wait(id, snapshot => snapshot.LiveRuns > 0);
        harness.Tasks.StopTask(id).ThrowIfError();

        TaskSnapshot canceled = harness.Settle(id);
        Assert.Equal(TaskState.Canceled, canceled.State);
        Assert.Equal(0, canceled.LiveRuns);
    }

    private const string ReviewOnPlanFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Steps": [
                { "Name": "规划", "Role": "Plan", "Gate": "Review" },
                { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"] },
                { "Name": "检查", "Role": "Check", "Scope": "PerItem", "From": ["规划", "实施"] }
              ]
            }
          ]
        }
        """;

    private const string StopOnRejectFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Steps": [
                { "Name": "规划", "Role": "Plan" },
                { "Name": "实施", "Role": "Implement", "Scope": "PerItem", "From": ["规划"] },
                { "Name": "检查", "Role": "Check", "Scope": "PerItem", "From": ["实施"], "OnReject": "Stop" }
              ]
            }
          ]
        }
        """;
}
