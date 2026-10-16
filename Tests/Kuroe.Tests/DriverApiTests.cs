using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Tool;
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
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rename(id, "文档补齐").ThrowIfError();

        Assert.Equal("文档补齐", environment.Snapshot(id).Title);
        Assert.Equal(1, environment.Registry.ClearSettled());
        Assert.Empty(environment.Registry.Snapshots());
    }

    [Fact]
    public void Submission_must_come_from_a_live_run_of_the_right_role()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        RunSnapshot implement = Assert.Single(done.Executables[1].Runs, run => run.Context.ItemIndex == 0);
        RunId implementRun = implement.Id;

        Assert.Contains("被拒绝", ToolResult.Render(environment.Submitter.SubmitItems(implementRun, """[{"Title":"甲"}]""")));
    }

    [Fact]
    public void Empty_plan_is_refused_and_leaves_the_step_unclosed()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.ItemsJson = "[]";

        TaskSnapshot blocked = environment.Settle(environment.Submit("补齐 README"));
        RunSnapshot plan = Assert.Single(blocked.Executables[0].Runs);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(environment.Executor.Submissions, text => text.Contains("被拒绝") && text.Contains("Title"));
        Assert.Contains(plan.Journal, entry => entry is ErrorEntry);
    }
}
