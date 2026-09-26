using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>叶子链推进：展开、收拢、放行、返工、未收口与取消。默认流程即展开后整体检查。</summary>
public sealed class FlowAdvanceTests
{
    [Fact]
    public void Plan_implement_funnel_check_completes_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Nodes[0].Runs);
        Assert.Equal(2, done.Nodes[1].Runs.Count);
        // 整体检查由单个 agent 汇拢全部条目实施
        Assert.Single(done.Nodes[2].Runs);
        Assert.Equal(3, done.FrontierNodes);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));
    }

    [Fact]
    public void Rejected_funnel_check_retries_all_items_with_findings()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = run => run.Context.Attempt > 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        // 两条目各实施两轮，整体检查两轮
        Assert.Equal(4, done.Nodes[1].Runs.Count);
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null), unit =>
        {
            Assert.Equal(2, unit.Attempts);
            Assert.Equal(UnitVerdict.Verified, unit.Verdict);
        });

        RunSnapshot retry = Assert.Single(done.Nodes[1].Runs,
            run => run.Context.ItemIndex == 0 && run.Context.Attempt == 2);
        Assert.Contains(retry.Context.Seed, message => message.Text.Contains("上一轮检查未通过"));
    }

    [Fact]
    public void Funnel_check_without_findings_is_rejected_and_blocks()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = _ => false;
        harness.Executor.FindingsForCheck = string.Empty;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(harness.Executor.Submissions, text => text.Contains("被拒绝：不通过时要列出问题"));
    }

    [Fact]
    public void Review_gate_parks_plan_until_approved()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewOnPlanFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot parked = harness.Settle(id);
        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Single(parked.Nodes[0].Runs);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Nodes[1].Runs.Count);
    }

    [Fact]
    public void Per_item_check_flow_checks_each_item_separately()
    {
        using KuroeHarness harness = KuroeHarness.Create(PerItemCheckFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        // 逐条检查：每个条目各一个检查 agent
        Assert.Equal(2, done.Nodes[2].Runs.Count);
        Assert.All(done.Units.Where(unit => unit.ItemIndex is not null),
            unit => Assert.Equal(UnitVerdict.Verified, unit.Verdict));
    }
    [Fact]
    public void Plan_step_without_submission_blocks_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPlan = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        RunSnapshot plan = Assert.Single(blocked.Nodes[0].Runs);
        Assert.Equal(RunState.Failed, plan.State);
        Assert.Contains(plan.Failures, failure => failure.Contains("未收口"));
    }

    [Fact]
    public void Stop_reject_action_parks_units_until_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create(StopOnRejectFlow);
        harness.Executor.CheckPasses = _ => false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        harness.Tasks.Rework(id, null).ThrowIfError();
        // 返工退回实施叶，重跑一轮实施后再次整体检查并停留
        TaskSnapshot reopened = harness.Wait(id, snapshot =>
            snapshot.Units.Any(unit => unit.ItemIndex == 0 && unit.Attempts > 1));

        Assert.Equal(2, Assert.Single(reopened.Units, unit => unit.ItemIndex == 0).Attempts);
    }

    [Fact]
    public void Cancel_after_block_settles_the_task()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.CheckPasses = _ => false;

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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime", "GetWeather"] },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan", "Gate": "Review" },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
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
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime", "GetWeather"] },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "整体检查", "Agent": "检查者", "Output": "Review", "From": ["制定计划", "分配执行"], "OnReject": "Stop" }
              ]
            }
          ]
        }
        """;

    private const string PerItemCheckFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Agents": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime", "GetWeather"] },
                { "Name": "检查者" }
              ],
              "Nodes": [
                { "Name": "制定计划", "Agent": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Agent": "执行者", "Mode": "PerItem", "From": ["制定计划"] },
                { "Name": "逐条检查", "Agent": "检查者", "Output": "Review", "Mode": "PerItem", "From": ["分配执行"], "OnReject": "Retry", "MaxAttempts": 2 }
              ]
            }
          ]
        }
        """;
}