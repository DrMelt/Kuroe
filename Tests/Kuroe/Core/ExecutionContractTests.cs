using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Tool;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Tool.Contract;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Contract;
using Xunit;

namespace Kuroe.Tests;

/// <summary>执行的接口契约：需交回的产出由交回工具的说明给出，查看工具报出本轮状态，上报无法完成使本轮停驻待返工。</summary>
public sealed class ExecutionContractTests
{
    [Fact]
    public void Declared_system_prompt_blocks_become_the_system_instruction()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.SystemPromptFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        IExecution execution = Assert.Single(environment.Runner.Started);
        Assert.Equal("固定背景一\n固定背景二", LlmSystemPrompt(execution));
    }

    /// <summary>本轮模型侧的系统指令：节点声明的恒定块。</summary>
    private static string? LlmSystemPrompt(IExecution execution) =>
        Assert.IsType<LlmNodeType>(execution.Input.Type).SystemPrompt();

    /// <summary>契约工具各自声明路径：交回工具的说明带端口清单，上报与查看工具不带。</summary>
    [Fact]
    public void Contract_tools_declare_their_own_paths()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        IExecution ported = Ported(environment);
        PortTool port = environment.PortTool;
        CannotCompleteTool cannotComplete = environment.CannotCompleteTool;
        StatusTool status = new(new ExecutionStatusReader(environment.Executions));

        IScopedTool[] tools = [port, cannotComplete, status];

        Assert.Equal(
            [ContractTools.Port, ContractTools.CannotComplete, ContractTools.Status],
            tools.Select(tool => Assert.Single(tool.ForExecution(ported.Id).Functions).Definition.Path.Value));

        // 未绑定执行的载体不写端口名，绑定执行后按该执行的声明列出
        Assert.DoesNotContain("结论", port.Functions.Single().Definition.Description, StringComparison.Ordinal);
        Assert.Contains("本节点需要交回的端口：结论、理由",
            Assert.Single(port.ForExecution(ported.Id).Functions).Definition.Description, StringComparison.Ordinal);
        Assert.Contains("本轮执行契约状态", Assert.Single(status.Functions).Definition.Description, StringComparison.Ordinal);
    }

    /// <summary>交回回执在产出齐备时说明已齐备。</summary>
    [Fact]
    public void A_collected_round_receipt_reports_the_outputs_as_complete()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("已记录 2 个端口的产出。本轮产出已齐备。", StringComparison.Ordinal));
    }

    /// <summary>上报无法完成：本轮停驻阻塞，原因写进失败，回执附带仍缺的端口，之后不再追问。</summary>
    [Fact]
    public void Cannot_complete_report_stops_without_asking_again()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.SubmitsPorts = false;
        environment.Runner.CannotComplete = true;

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);

        IExecution execution = Ported(environment);
        Assert.Contains("模型报告无法完成：缺少输入资料", string.Join('；', execution.Snapshot().Failures), StringComparison.Ordinal);
        Assert.Contains(environment.Runner.Submissions, text =>
            text.Contains("已记录本轮无法完成：缺少输入资料", StringComparison.Ordinal)
            && text.Contains("本轮仍需交回：端口「结论」、「理由」", StringComparison.Ordinal));
        Assert.Null(new ContractReminder(new ExecutionStatusReader(environment.Executions)).Text(execution.Id));
    }

    /// <summary>上报无法完成必须给出原因，缺原因时按拒绝交回，本轮照常收口。</summary>
    [Fact]
    public void Cannot_complete_report_without_a_reason_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.CannotComplete = true;
        environment.Runner.CannotCompleteReason = " ";

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("上报无法完成时必须给出原因", StringComparison.Ordinal));
    }

    /// <summary>不上报任何内容时，本轮按交回的产出收口。</summary>
    [Fact]
    public void Without_a_report_the_round_settles_on_what_came_back()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.DoesNotContain(environment.Runner.Submissions, text => text.Contains("无法完成", StringComparison.Ordinal));
    }

    /// <summary>查看工具绑定执行后报出本轮的缺口与节点状态。</summary>
    [Fact]
    public void Status_tool_reports_the_round_contract()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        StatusTool tool = new(new ExecutionStatusReader(environment.Executions));
        ToolFunction function = Assert.Single(tool.ForExecution(Ported(environment).Id).Functions);

        string text = function.Invoke(new ToolArguments(new Dictionary<string, object?>())).Value;

        Assert.Contains("本轮仍需交回：端口「结论」、「理由」。", text, StringComparison.Ordinal);
        Assert.Contains("节点状态：已阻塞", text, StringComparison.Ordinal);
    }

    /// <summary>声明了输出端口的那个执行，端口相关的系统指令与工具描述都在它身上。</summary>
    private static IExecution Ported(TestEnvironment environment) =>
        environment.Runner.Started.First(execution => execution.Input.OutputPorts.Count > 0);
}
