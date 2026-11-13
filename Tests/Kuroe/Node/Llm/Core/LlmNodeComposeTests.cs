using Kuroe.Execution.Shared;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>Llm 类型的模型侧内容：本轮要做的事、系统指令与能力面。</summary>
public sealed class LlmNodeComposeTests
{
    [Fact]
    public void Prompt_leads_the_instruction() =>
        Assert.Equal("围绕目标展开\n这是第 2 轮实施。",
            Typed(new LlmParameters { Prompt = "围绕目标展开" }).Instruction(Round()));

    [Fact]
    public void Without_a_declaration_the_round_remains() =>
        Assert.Equal("这是第 2 轮实施。", Typed(new LlmParameters()).Instruction(Round()));

    [Fact]
    public void A_whole_node_first_round_has_no_instruction() =>
        Assert.Empty(Typed(new LlmParameters()).Instruction(WholeNode()));

    [Fact]
    public void Blocks_become_the_system_instruction() =>
        Assert.Equal("恒定块一\n恒定块二", Typed(new LlmParameters { SystemPrompt = ["恒定块一", "恒定块二"] }).SystemPrompt());

    [Fact]
    public void Without_blocks_there_is_no_system_instruction() =>
        Assert.Null(Typed(new LlmParameters()).SystemPrompt());

    [Fact]
    public void Declaration_leads_the_capability_paths_and_contract_tools_follow() =>
        Assert.Equal(
            ["ReadFile", "SubmitPortValues", "ReportCannotComplete", "GetStatus"],
            Typed(new LlmParameters { Tools = [new ToolPath("ReadFile")] })
                .ToolPaths([Port("结论", ExecutionPortKind.Text)])
                .Select(path => path.Value));

    [Fact]
    public void Text_ports_grant_the_port_contract_tool() =>
        Assert.Equal(
            ["SubmitPortValues", "ReportCannotComplete", "GetStatus"],
            Typed(new LlmParameters())
                .ToolPaths([Port("结论", ExecutionPortKind.Text)])
                .Select(path => path.Value));

    [Fact]
    public void Signal_ports_grant_the_port_contract_tool() =>
        Assert.Equal(
            ["SubmitPortValues", "ReportCannotComplete", "GetStatus"],
            Typed(new LlmParameters())
                .ToolPaths([Port("完成", ExecutionPortKind.Signal)])
                .Select(path => path.Value));

    [Fact]
    public void Context_ports_keep_the_execution_channels() =>
        Assert.Equal(
            ["ReportCannotComplete", "GetStatus"],
            Typed(new LlmParameters())
                .ToolPaths([Port("上下文", ExecutionPortKind.Context)])
                .Select(path => path.Value));

    [Fact]
    public void A_node_without_output_ports_keeps_the_execution_channels() =>
        Assert.Equal(
            ["ReportCannotComplete", "GetStatus"],
            Typed(new LlmParameters()).ToolPaths([]).Select(path => path.Value));

    private static LlmNodeType Typed(LlmParameters parameters) => Assert.IsType<LlmNodeType>(LlmNodeTypes.Create(parameters));

    private static ExecutionOutputPort Port(string name, ExecutionPortKind kind) =>
        new(new ExecutionPortName(name), kind, kind != ExecutionPortKind.Context, false);

    /// <summary>第 2 轮执行的请求。</summary>
    private static ExecutionRequest Round() => Request(2);

    /// <summary>节点首次执行的请求。</summary>
    private static ExecutionRequest WholeNode() => Request(1);

    private static ExecutionRequest Request(int attempt) =>
        new(
            new ExecutionOwnerId("1"),
            new ExecutionTargetId("实施"),
            "Llm",
            attempt,
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            []);
}