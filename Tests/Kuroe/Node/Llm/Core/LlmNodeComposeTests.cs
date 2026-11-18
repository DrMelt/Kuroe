using Kuroe.Runner.Llm;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>Llm 声明到执行参数的内容组合：本轮要做的事与系统指令。</summary>
public sealed class LlmNodeComposeTests
{
    [Fact(DisplayName = "提示排在校验说明之前")]
    public void Prompt_leads_the_instruction() =>
        Assert.Equal("围绕目标展开\n这是第 2 轮实施。",
            Typed(new LlmParameters { Prompt = "围绕目标展开" }, 2).Instruction());

    [Fact(DisplayName = "没有声明时轮次保持不变")]
    public void Without_a_declaration_the_round_remains() =>
        Assert.Equal("这是第 2 轮实施。", Typed(new LlmParameters(), 2).Instruction());

    [Fact(DisplayName = "整节点首轮没有校验说明")]
    public void A_whole_node_first_round_has_no_instruction() =>
        Assert.Null(Typed(new LlmParameters(), 1).Instruction());

    [Fact(DisplayName = "块成为系统指令")]
    public void Blocks_become_the_system_instruction() =>
        Assert.Equal("恒定块一\n恒定块二", Typed(new LlmParameters { SystemPrompt = ["恒定块一", "恒定块二"] }, 1).SystemPromptText());

    [Fact(DisplayName = "没有块时没有系统指令")]
    public void Without_blocks_there_is_no_system_instruction() =>
        Assert.Null(Typed(new LlmParameters(), 1).SystemPromptText());

    private static LlmExecutionParameters Typed(LlmParameters parameters, int attempt) =>
        parameters.ToExecutionParameters(null, attempt: attempt);
}
