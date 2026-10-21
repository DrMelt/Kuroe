using Kuroe.Cli.Views;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Presentation;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>宿主侧的状态与时间呈现文本。</summary>
public sealed class ViewLabelsTests
{
    [Fact]
    public void State_labels_cover_every_body_state()
    {
        Assert.Equal("执行中", ViewLabels.Of(TaskState.Running));
        Assert.Equal("已阻塞", ViewLabels.Of(TaskState.Blocked));
        Assert.Equal("已完成", ViewLabels.Of(TaskState.Done));
        Assert.Equal("已取消", ViewLabels.Of(TaskState.Canceled));
    }

    [Fact]
    public void Input_port_label_shows_its_default()
    {
        InputPort port = new(new PortName("背景"), PortType.Text, "可选背景", "固定背景");

        Assert.Equal("背景：可选背景，默认 固定背景", port.Label());
    }

    [Fact]
    public void Node_state_labels_cover_the_scope()
    {
        Assert.Equal("执行中", ViewLabels.Of(NodeState.Running));
        Assert.Equal("待执行", ViewLabels.Of(NodeState.Pending));
        Assert.Equal("已完成", ViewLabels.Of(NodeState.Done));
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
    public void Execution_state_appends_progress_when_present()
    {
        Assert.Equal("执行中", ViewLabels.State(Execution("")));
        Assert.Equal("执行中（调用工具 GetTime）", ViewLabels.State(Execution("调用工具 GetTime")));
        Assert.Equal("已完成", ViewLabels.State(Execution("调用工具 GetTime") with { State = ExecutionState.Succeeded }));
    }

    [Fact]
    public void Style_of_state_picks_the_host_style()
    {
        Assert.Equal(Styles.Success, ViewLabels.StyleOf(TaskState.Done));
        Assert.Equal(Styles.Key, ViewLabels.StyleOf(TaskState.Running));
        Assert.Equal(Styles.Warning, ViewLabels.StyleOf(TaskState.Blocked));
        Assert.Equal(Styles.Hint, ViewLabels.StyleOf(TaskState.Canceled));
    }

    private static ExecutionSnapshot Execution(string progress) => new(
        new ExecutionId(1), Input(), ExecutionState.Running, progress,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, [], [], 0);

    private static ExecutionInput Input() => new()
    {
        Task = new TaskId(1),
        Node = NodePath.Of("制定计划"),
        Type = new StubNodeType(new NodeKind("Executable")),
    };
}
