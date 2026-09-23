using ErrorOr;
using Kuroe.Agent;
using Kuroe.Workflows;
using Xunit;

namespace Kuroe.Tests;

/// <summary>宿主直接调用的任务侧接口：采纳、改名、清理与提交校验。</summary>
public sealed class DriverApiTests
{
    [Fact]
    public void Adopt_writes_conclusion_into_task_dialogue()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);
        RunSnapshot implement = Assert.Single(done.Steps[1].Runs, run => run.Context.ItemIndex == 0);

        harness.Driver.Adopt(implement.Id).ThrowIfError();

        Assert.Contains(harness.Snapshot(id).Dialogue, entry =>
            entry is PromptEntry prompt && prompt.Text.Contains("实施产出"));
    }

    [Fact]
    public void Adopt_needs_a_finished_run_with_output()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPlan = false;

        TaskSnapshot blocked = harness.Settle(harness.Submit("补齐 README"));

        Error result = Assert.Single(harness.Driver.Adopt(blocked.Steps[0].Runs[0].Id).ErrorsOrEmptyList);
        Assert.Equal("Run.Adopt", result.Code);
    }

    [Fact]
    public void Title_and_clear_keep_the_list_in_step()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);
        harness.Driver.Rename(id, "文档补齐").ThrowIfError();

        Assert.Equal("文档补齐", harness.Snapshot(id).Title);
        Assert.Equal(1, harness.Registry.ClearFinished());
        Assert.Empty(harness.Registry.Snapshots());
        Assert.Null(harness.Registry.Active);
    }

    [Fact]
    public void Submission_must_come_from_a_live_run_of_the_right_role()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot implement = Assert.Single(done.Steps[1].Runs, run => run.Context.ItemIndex == 0);
        TurnScope scope = harness.Registry.FindRun(implement.Id).ThrowIfError().Scope;

        Assert.Contains("被拒绝", harness.Submitter.SubmitPlan(scope, """[{"Title":"甲"}]"""));
        Assert.Contains("被拒绝", harness.Submitter.SubmitVerdict(scope, true, string.Empty));
    }

    [Fact]
    public void Empty_plan_is_refused_and_leaves_the_step_unclosed()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.ItemsJson = "[]";

        TaskSnapshot blocked = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(blocked.Steps[0].Runs);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(harness.Executor.Submissions, text => text.Contains("被拒绝") && text.Contains("Title"));
        Assert.Contains(plan.Journal, entry => entry is ErrorEntry);
    }
}
