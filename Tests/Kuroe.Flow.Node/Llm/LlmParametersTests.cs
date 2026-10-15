using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Node.Llm.Tests;

/// <summary>Llm 参数的运行形状：节点要求、恒定块与能力面在本轮请求里的摆放顺序。</summary>
public sealed class LlmParametersTests
{
    [Fact]
    public void Prompt_leads_the_instruction()
    {
        RunRequest request = new LlmParameters { Prompt = "围绕目标展开" }.Compose(Materials());

        Assert.Equal("围绕目标展开\n本次只负责条目 1：甲\n这是第 2 轮实施。", request.Instruction);
        Assert.Equal("本轮接口", request.SystemPrompt);
    }

    [Fact]
    public void Blocks_lead_the_system_prompt_and_tools_lead_the_tool_set()
    {
        RunRequest request = new LlmParameters
        {
            Tools = [new CapabilityPath("ReadFile")],
            SystemPrompt = ["恒定块一", "恒定块二"],
        }.Compose(Materials());

        Assert.Equal("恒定块一\n恒定块二\n\n本轮接口", request.SystemPrompt);
        Assert.Equal(["ReadFile", "SubmitStatus"], request.Tools.Select(tool => tool.Value));
    }

    [Fact]
    public void Blocks_without_briefing_leave_no_trailing_blank_line() =>
        Assert.Equal("恒定块", new LlmParameters { SystemPrompt = ["恒定块"] }.Compose(new RunMaterials()).SystemPrompt);

    [Fact]
    public void Without_blocks_or_briefing_there_is_no_system_prompt() =>
        Assert.Null(new LlmParameters().Compose(new RunMaterials()).SystemPrompt);

    private static RunMaterials Materials() => new()
    {
        Steps = ["本次只负责条目 1：甲", "这是第 2 轮实施。"],
        Briefing = "本轮接口",
        ContractTools = [new CapabilityPath("SubmitStatus")],
    };
}