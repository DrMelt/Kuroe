using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Node.Llm.Tests.Executions;

/// <summary>产出契约的补交提示：缺端口时点名缺项与交回工具，齐备或已上报无法完成时不给提示。</summary>
public sealed class ContractReminderTests
{
    private static readonly PortName Conclusion = new("结论");
    private static readonly PortName Completion = new("完成");

    [Fact]
    public void Missing_port_values_remind_the_port_and_the_contract_tool()
    {
        ContractReminder reminder = new(new FakeReader(Status([Conclusion])));

        string text = Assert.IsType<string>(reminder.Text(new ExecutionId(1)));

        Assert.Contains("「结论」", text, StringComparison.Ordinal);
        Assert.Contains(ContractTools.Port, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Collected_outputs_give_no_reminder()
    {
        ContractReminder reminder = new(new FakeReader(Status([])));

        Assert.Null(reminder.Text(new ExecutionId(1)));
    }

    [Fact]
    public void Missing_signal_port_reminds_the_same_way()
    {
        ContractReminder reminder = new(new FakeReader(Status([Completion])));

        string text = Assert.IsType<string>(reminder.Text(new ExecutionId(1)));

        Assert.Contains("「完成」", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Cannot_complete_report_gives_no_reminder()
    {
        ContractReminder reminder = new(new FakeReader(Status([Conclusion], "缺少输入资料")));

        Assert.Null(reminder.Text(new ExecutionId(1)));
    }

    [Fact]
    public void Read_error_gives_no_reminder()
    {
        ContractReminder reminder = new(new FakeReader(Error.NotFound("Execution.NotFound", "执行不存在。")));

        Assert.Null(reminder.Text(new ExecutionId(1)));
    }

    private static ExecutionStatus Status(IReadOnlyList<PortName> outstanding, string? reason = null) =>
        new(NodePath.Of("实施"), 1, 3, NodeState.Running, outstanding, reason);

    private sealed class FakeReader(ErrorOr<ExecutionStatus> result) : IExecutionStatusReader
    {
        public ErrorOr<ExecutionStatus> Read(ExecutionId id) => result;
    }
}
