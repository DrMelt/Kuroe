using Kuroe.Console.Views;
using Kuroe.Presentation.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>任务树的渲染：直接构造任务快照，不提交任务。</summary>
public sealed class TaskTreeViewTests
{
    [Fact]
    public void Tree_prints_only_the_node_tree()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        new TaskTreeView(terminal, cli.Environment.Pendings).Print(Snapshot(FlowItemState.Pending));

        Assert.Contains("节点", output.Output);
        Assert.Contains("整体", output.Output);
        Assert.Contains("制定计划", output.Output);
        Assert.DoesNotContain("过程记录", output.Output);
        Assert.DoesNotContain('┌', output.Output);
    }

    [Fact]
    public void Tree_prints_the_blocked_reason()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        new TaskTreeView(terminal, cli.Environment.Pendings).Print(Snapshot(FlowItemState.Blocked));

        Assert.Contains("阻塞原因 节点失败原因。", output.Output);
    }

    [Fact]
    public void Tree_reads_the_node_state_from_the_snapshot()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        new TaskTreeView(terminal, cli.Environment.Pendings).Print(Snapshot(FlowItemState.Done));

        Assert.Contains("已完成", output.Output);
    }

    private static TaskSnapshot Snapshot(FlowItemState state) => new(
        new TaskId(1),
        "目标",
        new StartInputs([]),
        new FlowView(
            "默认",
            "graph",
            null,
            [],
            [
                new FlowStepView(
                    "整体/制定计划",
                    "制定计划",
                    "Llm",
                    state,
                    [],
                    [],
                    [],
                    100)
                {
                    GroupPath = "整体",
                    Block = state == FlowItemState.Blocked ? "节点失败原因。" : null,
                    CanRestore = false,
                },
            ],
            [
                new FlowGroupView("整体", "整体", ["整体/制定计划"])
                {
                    State = state,
                },
            ]),
        TaskState.Running,
        0,
        [],
        0,
        At,
        At);

    /// <summary>固定的快照时间。</summary>
    private static readonly DateTimeOffset At = new(2026, 1, 2, 15, 4, 5, TimeSpan.FromHours(8));
}
