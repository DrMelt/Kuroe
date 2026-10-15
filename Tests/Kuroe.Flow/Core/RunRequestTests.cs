using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph.Types;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>本轮请求的合成：引擎材料与类型参数的摆放顺序。</summary>
public sealed class RunRequestTests
{
    [Fact]
    public void Materials_become_the_request_as_is()
    {
        RunRequest request = RunRequest.From(Materials());

        Assert.Equal("本次只负责条目 1：甲\n这是第 2 轮实施。", request.Instruction);
        Assert.Equal("本轮接口", request.SystemPrompt);
        Assert.Equal(["SubmitStatus"], request.Tools.Select(tool => tool.Value));
    }

    [Fact]
    public void Materials_without_steps_leave_an_empty_instruction() =>
        Assert.Empty(RunRequest.From(new RunMaterials { Briefing = "本轮接口" }).Instruction);

    [Fact]
    public void Materials_without_briefing_leave_no_system_prompt()
    {
        RunRequest request = RunRequest.From(new RunMaterials { Steps = ["本次只负责条目 1：甲"] });

        Assert.Equal("本次只负责条目 1：甲", request.Instruction);
        Assert.Null(request.SystemPrompt);
    }

    [Fact]
    public void Llm_prompt_leads_the_instruction()
    {
        RunRequest request = new LlmParameters { Prompt = "围绕目标展开" }.Compose(Materials());

        Assert.Equal("围绕目标展开\n本次只负责条目 1：甲\n这是第 2 轮实施。", request.Instruction);
        Assert.Equal("本轮接口", request.SystemPrompt);
    }

    [Fact]
    public void Llm_blocks_lead_the_system_prompt_and_tools_lead_the_tool_set()
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
    public void Llm_blocks_without_briefing_leave_no_trailing_blank_line() =>
        Assert.Equal("恒定块", new LlmParameters { SystemPrompt = ["恒定块"] }.Compose(new RunMaterials()).SystemPrompt);

    [Fact]
    public void Llm_without_blocks_or_briefing_has_no_system_prompt() =>
        Assert.Null(new LlmParameters().Compose(new RunMaterials()).SystemPrompt);

    private static RunMaterials Materials() => new()
    {
        Steps = ["本次只负责条目 1：甲", "这是第 2 轮实施。"],
        Briefing = "本轮接口",
        ContractTools = [new CapabilityPath("SubmitStatus")],
    };
}