using Kuroe.Cli.Views;
using Kuroe.Presentation;
using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.Execution.Runs;
using Kuroe.Flows.Shared.RuntimeGraph;
using Kuroe.Flows.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>宿主侧的状态、条目与时间呈现文本。</summary>
public sealed class ViewLabelsTests
{
    [Fact]
    public void State_labels_cover_every_body_state()
    {
        Assert.Equal("执行中", ViewLabels.Of(TaskState.Running));
        Assert.Equal("待批准", ViewLabels.Of(TaskState.AwaitingApproval));
        Assert.Equal("已阻塞", ViewLabels.Of(TaskState.Blocked));
        Assert.Equal("已完成", ViewLabels.Of(TaskState.Done));
        Assert.Equal("已取消", ViewLabels.Of(TaskState.Canceled));
    }

    [Fact]
    public void Mode_and_gate_labels_cover_the_scope()
    {
        Assert.Equal("执行中", ViewLabels.Of(NodeState.Running));
        Assert.Equal("按条目", ViewLabels.Of(NodeMode.PerItem));
        Assert.Equal("整节点", ViewLabels.Of(NodeMode.Single));
        Assert.Equal("自动放行", ViewLabels.Of(NodeGate.Auto));
    }

    [Fact]
    public void Node_kind_labels_cover_the_kinds()
    {
        Assert.Equal("容器", InfoLabels.Of(NodeKind.Container));
        Assert.Equal("执行", InfoLabels.Of(NodeKind.Executable));
        Assert.Equal("输入", InfoLabels.Of(NodeKind.Input));
    }

    [Fact]
    public void Item_labels_number_from_one()
    {
        Assert.Equal("整体", ViewLabels.Item(null));
        Assert.Equal("条目 1", ViewLabels.Item(0));
        Assert.Equal("条目 3", ViewLabels.Item(2));
    }

    [Fact]
    public void Elapsed_lines_switch_on_the_minute_mark()
    {
        Assert.Equal("59秒", ViewLabels.Elapsed(TimeSpan.FromSeconds(59)));
        Assert.Equal("1分30秒", ViewLabels.Elapsed(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Clock_renders_local_time()
    {
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", ViewLabels.Clock(DateTimeOffset.Now));
        Assert.Equal("—", ViewLabels.Clock((DateTimeOffset?)null));
    }

    [Fact]
    public void Run_state_appends_progress_when_present()
    {
        Assert.Equal("执行中", ViewLabels.State(Run("")));
        Assert.Equal("执行中（调用工具 GetTime）", ViewLabels.State(Run("调用工具 GetTime")));
        Assert.Equal("已完成", ViewLabels.State(Run("调用工具 GetTime") with { State = RunState.Succeeded }));
    }

    [Fact]
    public void Style_of_state_picks_the_host_style()
    {
        Assert.Equal(Styles.Success, ViewLabels.StyleOf(TaskState.Done));
        Assert.Equal(Styles.Key, ViewLabels.StyleOf(TaskState.Running));
        Assert.Equal(Styles.Warning, ViewLabels.StyleOf(TaskState.AwaitingApproval));
        Assert.Equal(Styles.Warning, ViewLabels.StyleOf(TaskState.AwaitingInput));
        Assert.Equal(Styles.Warning, ViewLabels.StyleOf(TaskState.Blocked));
        Assert.Equal(Styles.Hint, ViewLabels.StyleOf(TaskState.Canceled));
    }

    private static RunSnapshot Run(string progress) => new(
        new RunId(1), Context(), RunState.Running, progress,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, [], [], 0);

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        NodeIndex = 0,
        NodeName = new NodeName("制定计划"),
        Instruction = "做",
    };
}
