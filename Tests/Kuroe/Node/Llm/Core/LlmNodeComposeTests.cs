using Kuroe.Runner.Llm;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>Llm 声明到执行参数的内容组合：本轮要做的事与系统指令。</summary>
public sealed class LlmNodeComposeTests
{
    [Fact]
    public void Prompt_leads_the_instruction() =>
        Assert.Equal("围绕目标展开\n这是第 2 轮实施。",
            Typed(new LlmParameters { Prompt = "围绕目标展开" }, 2).Instruction());

    [Fact]
    public void Without_a_declaration_the_round_remains() =>
        Assert.Equal("这是第 2 轮实施。", Typed(new LlmParameters(), 2).Instruction());

    [Fact]
    public void A_whole_node_first_round_has_no_instruction() =>
        Assert.Null(Typed(new LlmParameters(), 1).Instruction());

    [Fact]
    public void Blocks_become_the_system_instruction() =>
        Assert.Equal("恒定块一\n恒定块二", Typed(new LlmParameters { SystemPrompt = ["恒定块一", "恒定块二"] }, 1).SystemPromptText());

    [Fact]
    public void Without_blocks_there_is_no_system_instruction() =>
        Assert.Null(Typed(new LlmParameters(), 1).SystemPromptText());

    private static LlmExecutionParameters Typed(LlmParameters parameters, int attempt) =>
        parameters.ToExecutionParameters(null, attempt: attempt);
}
