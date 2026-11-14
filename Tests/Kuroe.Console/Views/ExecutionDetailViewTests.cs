using Kuroe.Console.Views;
using Kuroe.Execution.Shared;
using Kuroe.Node.Llm;
using Kuroe.Presentation.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>执行详情的渲染：直接构造执行快照，断言契约端口区与输入取值区。</summary>
public sealed class ExecutionDetailViewTests
{
    [Fact]
    public void Llm_execution_prints_the_required_ports()
    {
        Terminal terminal = NewTerminal(out TestConsole output);
        TaskSnapshot task = SnapshotTask();
        ExecutionSnapshot execution = Snapshot(
            [
                new ExecutionOutputPort(new ExecutionPortName("结论"), ExecutionPortKind.Text, true, true, "是否通过"),
                new ExecutionOutputPort(new ExecutionPortName("信号"), ExecutionPortKind.Signal, true, true, "放行信号"),
                new ExecutionOutputPort(new ExecutionPortName("报告"), ExecutionPortKind.Text, true, false, "返工问题清单"),
            ]);

        new ExecutionDetailView(terminal).Print(task, execution);

        Assert.Contains("契约端口 3 个", output.Output);
        Assert.Contains("类型", output.Output);
        Assert.Contains("模式", output.Output);
        Assert.Contains("说明", output.Output);
        Assert.Contains("结论", output.Output);
        Assert.Contains("Text", output.Output);
        Assert.Contains("Signal", output.Output);
        Assert.Contains("Trigger", output.Output);
        Assert.Contains("是否通过", output.Output);
        Assert.Contains("信号", output.Output);
        Assert.Contains("报告", output.Output);
        Assert.Contains("Plain", output.Output);
        Assert.Contains("返工问题清单", output.Output);
    }

    [Fact]
    public void Llm_execution_without_required_ports_reports_none()
    {
        Terminal terminal = NewTerminal(out TestConsole output);

        new ExecutionDetailView(terminal).Print(SnapshotTask(), Snapshot([]));

        Assert.Contains("契约端口 0 个", output.Output);
        Assert.Contains("本次执行没有需要交回的端口。", output.Output);
    }

    [Fact]
    public void Execution_of_another_kind_omits_the_section()
    {
        Terminal terminal = NewTerminal(out TestConsole output);
        ExecutionSnapshot execution = Snapshot(
            [new ExecutionOutputPort(new ExecutionPortName("结论"), ExecutionPortKind.Text, true, false)],
            kind: "Command");

        new ExecutionDetailView(terminal).Print(SnapshotTask(), execution);

        Assert.DoesNotContain("契约端口", output.Output);
    }

    [Fact]
    public void Input_values_show_source_port_and_truncation_hint()
    {
        Terminal terminal = NewTerminal(out TestConsole output);
        string longText = new('字', 71);
        ExecutionSnapshot execution = Snapshot(
            [],
            inputs:
            [
                new ExecutionInputValue(
                    new ExecutionPortName("实施要求"),
                    longText,
                    "节点「上游」常量端口「实施要求」"),
                new ExecutionInputValue(
                    new ExecutionPortName("检查报告"),
                    "无",
                    "节点「节点」的端口「检查报告」默认值"),
            ]);

        new ExecutionDetailView(terminal).Print(SnapshotTask(), execution);

        Assert.Contains("来源", output.Output);
        Assert.Contains("端口", output.Output);
        Assert.Contains("节点「上游」常量端口「实施要求」", output.Output);
        Assert.Contains("节点「节点」的端口「检查报告」默认值", output.Output);
        Assert.DoesNotContain("节点「上游」常量端口「实施要求」 · 实施要求", output.Output);
        Assert.Equal(70, output.Output.Count(character => character == '字'));
        Assert.Contains("\u001b[38;5;8m（已截断）", output.Output);
        Assert.DoesNotContain("…", output.Output);
    }

    private static ExecutionSnapshot Snapshot(
        IReadOnlyList<ExecutionOutputPort> ports,
        string kind = "Llm",
        IReadOnlyList<ExecutionInputValue>? inputs = null)
    {
        ExecutionRequest request = new(
            new ExecutionOwnerId("1"),
            new ExecutionTargetId("制定计划"),
            kind,
            1,
            [],
            inputs ?? [],
            new Dictionary<ExecutionPortName, string>(),
            ports);

        return new ExecutionSnapshot(
            new ExecutionId(1),
            request,
            ExecutionState.Succeeded,
            string.Empty,
            At,
            At,
            At,
            [],
            [],
            ExecutionUsage.None,
            null,
            ExecutionContract.Empty,
            null,
            null);
    }

    private static TaskSnapshot SnapshotTask() => new(
        new TaskId(1),
        "目标",
        new StartInputs([]),
        new FlowView("默认", "graph", null, [], [], []),
        TaskState.Running,
        0,
        [],
        0,
        At,
        At);

    /// <summary>建一套写 ANSI 序列的测试终端，用于断言提示颜色。</summary>
    private static Terminal NewTerminal(out TestConsole output)
    {
        TestConsole stdout = new();
        stdout.EmitAnsiSequences();
        TestConsole stderr = new();
        stderr.EmitAnsiSequences();
        output = stdout;

        return new Terminal(stdout, stderr);
    }

    /// <summary>固定的快照时间。</summary>
    private static readonly DateTimeOffset At = new(2026, 1, 2, 15, 4, 5, TimeSpan.FromHours(8));
}