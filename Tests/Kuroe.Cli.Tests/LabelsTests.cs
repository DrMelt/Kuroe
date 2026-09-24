using Kuroe.Agent.Runs;
using Kuroe.Cli.Views;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>宿主侧的状态、条目与时间呈现文本。</summary>
public sealed class LabelsTests
{
    [Fact]
    public void State_labels_cover_every_body_state()
    {
        Assert.Equal("执行中", Labels.Of(TaskState.Running));
        Assert.Equal("待批准", Labels.Of(TaskState.AwaitingApproval));
        Assert.Equal("已阻塞", Labels.Of(TaskState.Blocked));
        Assert.Equal("已完成", Labels.Of(TaskState.Done));
        Assert.Equal("已取消", Labels.Of(TaskState.Canceled));
    }

    [Fact]
    public void Unit_names_cover_verdict_and_scope()
    {
        Assert.Equal("推进中", Labels.Of(UnitState.Working));
        Assert.Equal("未检查", Labels.Of(UnitVerdict.NotChecked));
        Assert.Equal("退回返工", Labels.Of(RejectAction.Retry));
        Assert.Equal("按条目", Labels.Of(StepScope.PerItem));
        Assert.Equal("自动放行", Labels.Of(StepGate.Auto));
    }

    [Fact]
    public void Item_labels_number_from_one()
    {
        Assert.Equal("整步", Labels.Item(null));
        Assert.Equal("条目 1", Labels.Item(0));
        Assert.Equal("条目 3", Labels.Item(2));
    }

    [Fact]
    public void Elapsed_lines_switch_on_the_minute_mark()
    {
        Assert.Equal("59秒", Labels.Elapsed(TimeSpan.FromSeconds(59)));
        Assert.Equal("1分30秒", Labels.Elapsed(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Clock_renders_local_time()
    {
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", Labels.Clock(DateTimeOffset.Now));
        Assert.Equal("—", Labels.Clock((DateTimeOffset?)null));
    }

    [Fact]
    public void Run_state_appends_progress_when_present()
    {
        Assert.Equal("执行中", Labels.State(Run("")));
        Assert.Equal("执行中（调用工具 GetTime）", Labels.State(Run("调用工具 GetTime")));
        Assert.Equal("已完成", Labels.State(Run("调用工具 GetTime") with { State = RunState.Succeeded }));
    }

    private static RunSnapshot Run(string progress) => new(
        new RunId(1), Context(), RunState.Running, progress,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, [], [], 0);

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        Role = RunRole.Plan,
        StepIndex = 0,
        StepName = "规划",
        Instruction = "做",
        Model = "fake",
    };
}