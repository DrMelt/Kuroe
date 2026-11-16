using Kuroe.Execution.Shared;
using Kuroe.Runner;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>Llm 声明到执行参数的内容组合：本轮要做的事与系统指令。</summary>
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
        Assert.Null(Typed(new LlmParameters()).Instruction(WholeNode()));

    [Fact]
    public void Blocks_become_the_system_instruction() =>
        Assert.Equal("恒定块一\n恒定块二", Typed(new LlmParameters { SystemPrompt = ["恒定块一", "恒定块二"] }).SystemPromptText());

    [Fact]
    public void Without_blocks_there_is_no_system_instruction() =>
        Assert.Null(Typed(new LlmParameters()).SystemPromptText());

    private static LlmExecutionParameters Typed(LlmParameters parameters) => parameters.ToExecutionParameters(null);

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