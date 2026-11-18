using ErrorOr;
using Kuroe.Tool;
using Xunit;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Runner.Llm.Tests.Executions;

/// <summary>产出契约的补交提示：缺端口时点名缺项与交回工具，齐备或已上报无法完成时不给提示。</summary>
public sealed class ContractReminderTests
{
    private static readonly ExecutionPortName Conclusion = new("结论");
    private static readonly ExecutionPortName Completion = new("完成");

    [Fact]
    public void Missing_port_values_remind_the_port_and_the_contract_tool()
    {
        ContractReminder reminder = new(new FakeEntry(Status([Conclusion])));

        string text = Assert.IsType<string>(reminder.Text());

        Assert.Contains("「结论」", text, StringComparison.Ordinal);
        Assert.Contains(ContractTools.Port, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Collected_outputs_give_no_reminder()
    {
        ContractReminder reminder = new(new FakeEntry(Status([])));

        Assert.Null(reminder.Text());
    }

    [Fact]
    public void Missing_signal_port_reminds_the_same_way()
    {
        ContractReminder reminder = new(new FakeEntry(Status([Completion])));

        string text = Assert.IsType<string>(reminder.Text());

        Assert.Contains("「完成」", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Cannot_complete_report_gives_no_reminder()
    {
        ContractReminder reminder = new(new FakeEntry(Status([Conclusion], "缺少输入资料")));

        Assert.Null(reminder.Text());
    }

    private static ExecutionStatus Status(
        IReadOnlyList<ExecutionPortName> outstanding,
        string? reason = null) =>
        new(new ExecutionTargetId("实施"), ExecutionState.Running, outstanding, reason);

    private sealed class FakeEntry(ExecutionStatus status) : IExecutionRequestEntry
    {
        public ExecutionId Id => new(1);

        ExecutionRequest IExecutionRequestEntry.Request => throw new NotSupportedException();

        public ExecutionStatus ReadStatus() => status;

        public ErrorOr<ExecutionStatus> SubmitOutputs(IReadOnlyList<ExecutionOutputValue> values) =>
            throw new NotSupportedException();

        public ErrorOr<ExecutionStatus> ReportCannotComplete(string reason) =>
            throw new NotSupportedException();
    }
}
