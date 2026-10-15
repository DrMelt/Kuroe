using ErrorOr;
using Kuroe.Flow.Execution.Turns;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Flow.Shared.RuntimeGraph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>宿主直接调用的任务侧接口：改名、清理与提交校验。</summary>
public sealed class DriverApiTests
{
    [Fact]
    public void Title_and_clear_keep_the_list_in_step()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);
        harness.Tasks.Rename(id, "文档补齐").ThrowIfError();

        Assert.Equal("文档补齐", harness.Snapshot(id).Title);
        Assert.Equal(1, harness.Registry.ClearSettled());
        Assert.Empty(harness.Registry.Snapshots());
    }

    [Fact]
    public void Submission_must_come_from_a_live_run_of_the_right_role()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskSnapshot done = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot implement = Assert.Single(done.Executables[1].Runs, run => run.Context.ItemIndex == 0);
        RunId implementRun = implement.Id;

        Assert.Contains("被拒绝", ToolResult.Render(harness.Submitter.SubmitItems(implementRun, """[{"Title":"甲"}]""")));
    }

    [Fact]
    public void Empty_plan_is_refused_and_leaves_the_step_unclosed()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.ItemsJson = "[]";

        TaskSnapshot blocked = harness.Settle(harness.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(blocked.Executables[0].Runs);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(harness.Executor.Submissions, text => text.Contains("被拒绝") && text.Contains("Title"));
        Assert.Contains(plan.Journal, entry => entry is ErrorEntry);
    }
}
