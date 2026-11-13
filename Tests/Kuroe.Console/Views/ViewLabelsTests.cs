using Kuroe.Console.Views;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Presentation.Shared;
using ExecutionUsage = Kuroe.Execution.Shared.ExecutionUsage;
using TokenUsage = Kuroe.Execution.Shared.TokenUsage;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;
using Kuroe.Flow.TestSupport;
using Spectre.Console;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>宿主侧的状态与时间呈现文本。</summary>
public sealed class ViewLabelsTests
{
    [Fact]
    public void State_labels_cover_every_body_state()
    {
        Assert.Equal("执行中", ViewLabels.Of(TaskState.Running));
        Assert.Equal("已阻塞", ViewLabels.Of(TaskState.Blocked));
        Assert.Equal("已暂停", ViewLabels.Of(TaskState.Paused));
        Assert.Equal("已完成", ViewLabels.Of(TaskState.Done));
        Assert.Equal("已终止", ViewLabels.Of(TaskState.Terminated));
    }

    [Fact]
    public void Input_port_label_shows_its_default()
    {
        InputPort port = new(new PortName("背景"), PortTypes.Text, "可选背景", "固定背景");

        Assert.Equal("背景：可选背景，默认 固定背景", port.Label());
    }

    [Fact]
    public void Node_state_labels_cover_the_scope()
    {
        Assert.Equal("执行中", ViewLabels.Of(FlowNodeState.Running));
        Assert.Equal("待执行", ViewLabels.Of(FlowNodeState.Pending));
        Assert.Equal("已完成", ViewLabels.Of(FlowNodeState.Done));
    }

    [Fact]
    public void Elapsed_lines_switch_on_the_minute_mark()
    {
        Assert.Equal("59秒", ViewLabels.Elapsed(TimeSpan.FromSeconds(59)));
        Assert.Equal("1分30秒", ViewLabels.Elapsed(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Context_shows_the_latest_request_and_falls_back_when_unknown()
    {
        ExecutionUsage usage = new(2, 0, new TokenUsage(150, 30, 180, null, null)) { ContextTokens = 90 };

        Assert.Equal("90", ViewLabels.Context(usage));
        Assert.Equal("未提供", ViewLabels.Context(ExecutionUsage.None));
    }

    [Fact]
    public void Token_entries_show_shares_and_group_subsets()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(100, 20, 120, 80, 5));

        IReadOnlyList<(string Label, string Value)> entries = ViewLabels.TokenEntries(usage);

        Assert.Equal(3, entries.Count);
        Assert.Equal(("输入", "100（83%），缓存输入 80（占输入 80%）"), entries[0]);
        Assert.Equal(("输出", "20（17%），推理 5（占输出 25%）"), entries[1]);
        Assert.Equal(("合计", "120"), entries[2]);
    }

    [Fact]
    public void Execution_state_appends_progress_when_present()
    {
        Assert.Equal("执行中", ViewLabels.State(Execution("")));
        Assert.Equal("执行中（调用工具 GetTime）", ViewLabels.State(Execution("调用工具 GetTime")));
        Assert.Equal("执行中（输出）", ViewLabels.State(Execution("输出")));
        Assert.Equal("已完成", ViewLabels.State(Execution("调用工具 GetTime") with { State = ExecutionState.Succeeded }));
    }

    [Fact]
    public void Style_of_state_picks_the_host_style()
    {
        Assert.Equal(Styles.Success, ViewLabels.StyleOf(TaskState.Done));
        Assert.Equal(Styles.Key, ViewLabels.StyleOf(TaskState.Running));
        Assert.Equal(Styles.Warning, ViewLabels.StyleOf(TaskState.Blocked));
        Assert.Equal(Styles.Warning, ViewLabels.StyleOf(TaskState.Paused));
        Assert.Equal(Styles.Hint, ViewLabels.StyleOf(TaskState.Terminated));
    }

    [Fact]
    public void Style_of_node_state_picks_the_host_style()
    {
        Assert.Equal(Styles.Success, ViewLabels.StyleOf(FlowNodeState.Done));
        Assert.Equal(Styles.Key, ViewLabels.StyleOf(FlowNodeState.Running));
        Assert.Equal(Styles.Warning, ViewLabels.StyleOf(FlowNodeState.Blocked));
        Assert.Equal(Styles.Hint, ViewLabels.StyleOf(FlowNodeState.Pending));
    }

    [Fact]
    public void Style_of_execution_state_picks_the_host_style()
    {
        Assert.Equal(Styles.Success, ViewLabels.StyleOf(ExecutionState.Succeeded));
        Assert.Equal(Styles.Error, ViewLabels.StyleOf(ExecutionState.Failed));
        Assert.Equal(Styles.Key, ViewLabels.StyleOf(ExecutionState.Running));
        Assert.Equal(Styles.Hint, ViewLabels.StyleOf(ExecutionState.Interrupted));
    }

    [Fact]
    public void Style_of_journal_level_picks_the_host_style()
    {
        Assert.Equal(Style.Plain, ViewLabels.StyleOf(JournalLevel.Information));
        Assert.Equal(Styles.Warning, ViewLabels.StyleOf(JournalLevel.Warning));
        Assert.Equal(Styles.Error, ViewLabels.StyleOf(JournalLevel.Error));
    }

    [Fact]
    public void One_line_flattens_newlines_and_truncates()
    {
        Assert.Equal("甲 乙", ViewLabels.OneLine("甲\n乙", 10));
        Assert.Equal("甲乙丙…", ViewLabels.OneLine("甲乙丙丁", 3));
    }

    [Fact]
    public void Start_port_label_includes_its_fixed_value()
    {
        Port port = new(new PortName("风格"), PortTypes.Text, value: "技术文档风格");

        Assert.Equal("风格=技术文档风格", port.StartLabel());
    }

    private static FlowExecutionView Execution(string progress) => new(
        1, ExecutionState.Running, progress, [])
    {
        Round = 1,
        CreatedAt = TestTime.At,
        StartedAt = TestTime.At,
    };
}
