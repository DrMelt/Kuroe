using ErrorOr;
using Kuroe.Ability.Tool;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Ability.Runner.Llm.Tests.Executions;

/// <summary>产出契约的补交提示：缺端口时点名缺项与交回工具，齐备或已上报无法完成时不给提示。</summary>
public sealed class ContractReminderTests
{
    private static readonly ExecutionPortName Conclusion = new("结论");
    private static readonly ExecutionPortName Completion = new("完成");

    [Fact(DisplayName = "缺少端口值提醒端口与契约工具")]
    public void Missing_port_values_remind_the_port_and_the_contract_tool()
    {
        ContractReminder reminder = new(new FakeEntry(Status([Conclusion])));

        string text = Assert.IsType<string>(reminder.Text());

        Assert.Contains("「结论」", text, StringComparison.Ordinal);
        Assert.Contains(ContractTools.Port, text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "已收口输出不给出提醒")]
    public void Collected_outputs_give_no_reminder()
    {
        ContractReminder reminder = new(new FakeEntry(Status([])));

        Assert.Null(reminder.Text());
    }

    [Fact(DisplayName = "缺少信号端口同样提醒")]
    public void Missing_signal_port_reminds_the_same_way()
    {
        ContractReminder reminder = new(new FakeEntry(Status([Completion])));

        string text = Assert.IsType<string>(reminder.Text());

        Assert.Contains("「完成」", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "无法完成报告不给出提醒")]
    public void Cannot_complete_report_gives_no_reminder()
    {
        ContractReminder reminder = new(new FakeEntry(Status([Conclusion], "缺少输入资料")));

        Assert.Null(reminder.Text());
    }

    private static ExecutionStatus Status(
        IReadOnlyList<ExecutionPortName> outstanding,
        string? reason = null) =>
        new(new ExecutionStepId("实施"), ExecutionState.Running, outstanding, reason);

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
