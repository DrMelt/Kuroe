using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Tool.Contract;
using Kuroe.Flow.Runtime.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>执行的接口契约：需交回的产出与可选分支由契约工具的说明给出，执行状态由 SubmitStatus 声明并与收口判定配合。</summary>
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

    [Fact]
    public void Status_tool_description_states_values_and_required_ports()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        StatusTool tool = new(environment.Runner.StatusSubmitter!, environment.Registry);

        // 未绑定执行的载体不写端口名，绑定执行后按该执行的声明列出
        Assert.DoesNotContain("结论", tool.Functions.Single().Definition.Description, StringComparison.Ordinal);
        ToolFunction declared = Assert.Single(tool.ForExecution(Ported(environment).Id).Functions);
        Assert.Contains("completed", declared.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("本节点需要交回的端口：结论、理由", declared.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Receipts_list_what_is_still_missing()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("本轮仍需交回：端口「结论」、「理由」", StringComparison.Ordinal));
        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("本轮状态：completed", StringComparison.Ordinal));
    }

    [Fact]
    public void Blocked_verdict_stops_without_asking_again()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.SubmitsPorts = false;
        environment.Runner.VerdictStatus = "blocked";
        environment.Runner.VerdictReason = "缺少输入资料";

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);

        IExecution execution = Ported(environment);
        Assert.Contains("模型报告无法完成：缺少输入资料", string.Join('；', execution.Snapshot().Failures), StringComparison.Ordinal);
        Assert.Null(new ExecutionReminder(environment.Registry).Reminder(execution));
    }

    [Fact]
    public void Blocked_verdict_without_reason_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.VerdictStatus = "blocked";

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("blocked 时必须给出 reason", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_status_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.VerdictStatus = "done";

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("status 只能取 completed 或 blocked", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_verdict_ports_still_decide()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.SubmitsVerdict = false;

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("本轮状态未提交", StringComparison.Ordinal));
    }

    /// <summary>声明了输出端口的那个执行，端口相关的系统指令与工具描述都在它身上。</summary>
    private static IExecution Ported(TestEnvironment environment) =>
        environment.Runner.Started.First(execution => execution.Input.OutputPorts.Count > 0);
}
