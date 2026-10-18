using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Tool;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Workflows.Tools.Builtin;
using Kuroe.Flow.Execution.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>run 的接口契约：输入端口与需交回的产出写进本轮接口说明，执行状态由 SubmitStatus 声明并与收口判定配合。</summary>
public sealed class RunContractTests
{
    [Fact]
    public void System_prompt_names_input_and_output_ports()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        string prompt = Ported(environment).Context.Briefing;

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
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.SystemPromptFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        IRun run = Assert.Single(environment.Executor.Started);
        Assert.StartsWith("固定背景一\n固定背景二", LlmInstructions(run), StringComparison.Ordinal);
    }

    /// <summary>本轮模型侧的系统指令：节点的恒定块与本轮接口说明。</summary>
    private static string LlmInstructions(IRun run) =>
        Assert.IsType<LlmNodeType>(run.Context.Type).Instructions(run.Context.Briefing) ?? string.Empty;

    [Fact]
    public void Upstream_ports_are_listed_as_inputs()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        IRun follow = environment.Executor.Started.Single(run => run.Context.Node.Label.Value == "实施");

        Assert.Contains("节点「分析」端口「结论」", follow.Context.Briefing, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_tool_description_states_values_and_required_ports()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        StatusTool tool = new(environment.Executor.StatusSubmitter!, environment.Registry);

        // 未绑定回合的载体不写端口名，绑定回合后按该 run 的声明列出
        Assert.DoesNotContain("结论", tool.Functions.Single().Definition.Description, StringComparison.Ordinal);
        ToolFunction declared = Assert.Single(tool.ForTurn(Ported(environment).Scope).Functions);
        Assert.Contains("completed", declared.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("本节点需要交回的端口：结论、理由", declared.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Receipts_list_what_is_still_missing()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Executor.SubmitsPorts = false;

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        Assert.Contains(environment.Executor.Submissions,
            text => text.Contains("本轮仍需交回：端口「结论」、「理由」", StringComparison.Ordinal));
        Assert.Contains(environment.Executor.Submissions,
            text => text.Contains("本轮状态：completed", StringComparison.Ordinal));
    }

    [Fact]
    public void Blocked_verdict_stops_without_asking_again()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Executor.SubmitsPorts = false;
        environment.Executor.VerdictStatus = "blocked";
        environment.Executor.VerdictReason = "缺少输入资料";

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);

        IRun run = Ported(environment);
        Assert.Contains("模型报告无法完成：缺少输入资料", string.Join('；', run.Snapshot().Failures), StringComparison.Ordinal);
        Assert.Null(new RunReminder(environment.Registry).Reminder(run));
    }

    [Fact]
    public void Blocked_verdict_without_reason_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Executor.VerdictStatus = "blocked";

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Contains(environment.Executor.Submissions,
            text => text.Contains("blocked 时必须给出 reason", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_status_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Executor.VerdictStatus = "done";

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        Assert.Contains(environment.Executor.Submissions,
            text => text.Contains("status 只能取 completed 或 blocked", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_verdict_ports_still_decide()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Executor.SubmitsVerdict = false;

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Contains(environment.Executor.Submissions,
            text => text.Contains("本轮状态未提交", StringComparison.Ordinal));
    }

    /// <summary>声明了输出端口的那个 run，端口相关的系统指令与工具描述都在它身上。</summary>
    private static IRun Ported(TestEnvironment environment) =>
        environment.Executor.Started.First(run => run.Context.OutputPorts.Count > 0);
}
