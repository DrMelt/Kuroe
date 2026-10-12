using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Tools.Builtin;
using Kuroe.Workflows.Execution.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>run 的接口契约：输入端口与需交回的产出写进系统指令，执行状态由 SubmitStatus 声明并与收口判定配合。</summary>
public sealed class RunContractTests
{
    [Fact]
    public void System_prompt_names_input_and_output_ports()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        string prompt = Ported(harness).Context.SystemPrompt ?? string.Empty;

        Assert.Contains("输入端口：", prompt, StringComparison.Ordinal);
        Assert.Contains("流程起点端口「目标」", prompt, StringComparison.Ordinal);
        Assert.Contains("需要交回：", prompt, StringComparison.Ordinal);
        Assert.Contains("输出端口 结论、理由", prompt, StringComparison.Ordinal);
        Assert.Contains("SubmitPortValues", prompt, StringComparison.Ordinal);
        Assert.Contains("SubmitStatus", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Declared_system_prompt_blocks_stay_in_front()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.SystemPromptFlow);

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        Run run = Assert.Single(harness.Executor.Started);
        Assert.StartsWith("固定背景一\n固定背景二", run.Context.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Upstream_ports_are_listed_as_inputs()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        Run follow = harness.Executor.Started.Single(run => run.Context.NodeName.Value == "实施");

        Assert.Contains("节点「分析」端口「结论」", follow.Context.SystemPrompt ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_tool_description_states_values_and_required_ports()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        StatusTool tool = new(harness.Executor.StatusSubmitter!, harness.Registry);

        // 未绑定回合的载体不写端口名，绑定回合后按该 run 的声明列出
        Assert.DoesNotContain("结论", tool.Functions.Single().Description, StringComparison.Ordinal);
        ToolFunction declared = Assert.Single(tool.ForTurn(Ported(harness).Scope).Functions);
        Assert.Contains("completed", declared.Description, StringComparison.Ordinal);
        Assert.Contains("本节点需要交回的端口：结论、理由", declared.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Receipts_list_what_is_still_missing()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);
        harness.Executor.SubmitsPorts = false;

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        Assert.Contains(harness.Executor.Submissions,
            text => text.Contains("本轮仍需交回：端口「结论」、「理由」", StringComparison.Ordinal));
        Assert.Contains(harness.Executor.Submissions,
            text => text.Contains("本轮状态：completed", StringComparison.Ordinal));
    }

    [Fact]
    public void Blocked_verdict_stops_without_asking_again()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);
        harness.Executor.SubmitsPorts = false;
        harness.Executor.VerdictStatus = "blocked";
        harness.Executor.VerdictReason = "缺少输入资料";

        TaskId id = harness.Submit("目标");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);

        Run run = Ported(harness);
        Assert.Contains("模型报告无法完成：缺少输入资料", string.Join('；', run.Snapshot().Failures), StringComparison.Ordinal);
        Assert.Null(new RunReminder(harness.Registry).Reminder(run));
    }

    [Fact]
    public void Blocked_verdict_without_reason_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);
        harness.Executor.VerdictStatus = "blocked";

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Contains(harness.Executor.Submissions,
            text => text.Contains("blocked 时必须给出 reason", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_status_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);
        harness.Executor.VerdictStatus = "done";

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        Assert.Contains(harness.Executor.Submissions,
            text => text.Contains("status 只能取 completed 或 blocked", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_verdict_ports_still_decide()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);
        harness.Executor.SubmitsVerdict = false;

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Contains(harness.Executor.Submissions,
            text => text.Contains("本轮状态未提交", StringComparison.Ordinal));
    }

    /// <summary>声明了输出端口的那个 run，端口相关的系统指令与工具描述都在它身上。</summary>
    private static Run Ported(KuroeHarness harness) =>
        harness.Executor.Started.First(run => run.Context.OutputPorts.Count > 0);
}
