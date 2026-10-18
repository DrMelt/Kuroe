using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>Llm 类型的模型侧内容：本轮要做的事、系统指令与能力面。</summary>
public sealed class LlmNodeComposeTests
{
    [Fact]
    public void Prompt_leads_the_instruction()
    {
        string instruction = Typed(new LlmParameters { Prompt = "围绕目标展开" }).Compose(Materials());

        Assert.Equal("围绕目标展开\n本次只负责条目 1：甲\n这是第 2 轮实施。", instruction);
    }

    [Fact]
    public void Without_a_declaration_the_materials_become_the_instruction() =>
        Assert.Equal("本次只负责条目 1：甲\n这是第 2 轮实施。", LlmNodeTypes.Create(null).Compose(Materials()));

    [Fact]
    public void Blocks_lead_the_system_instruction_and_the_briefing_follows() =>
        Assert.Equal(
            "恒定块一\n恒定块二\n\n本轮接口",
            Typed(new LlmParameters { SystemPrompt = ["恒定块一", "恒定块二"] }).Instructions("本轮接口"));

    [Fact]
    public void Blocks_without_briefing_leave_no_trailing_blank_line() =>
        Assert.Equal("恒定块", Typed(new LlmParameters { SystemPrompt = ["恒定块"] }).Instructions(string.Empty));

    [Fact]
    public void Without_blocks_or_briefing_there_is_no_system_instruction() =>
        Assert.Null(Typed(new LlmParameters()).Instructions(string.Empty));

    [Fact]
    public void Declaration_leads_the_capability_paths_and_contract_tools_follow() =>
        Assert.Equal(
            ["ReadFile", "SubmitPortValues", "SubmitStatus"],
            Typed(new LlmParameters { Tools = [new ToolPath("ReadFile")] })
                .ToolPaths([new Port(new PortName("结论"), PortType.Text)])
                .Select(path => path.Value));

    [Fact]
    public void Work_items_and_text_ports_each_grant_their_contract_tool() =>
        Assert.Equal(
            ["SubmitWorkItems", "SubmitPortValues", "SubmitStatus"],
            Typed(new LlmParameters())
                .ToolPaths([
                    new Port(new PortName("拆分"), PortType.WorkItems),
                    new Port(new PortName("结论"), PortType.Text),
                ])
                .Select(path => path.Value));

    [Fact]
    public void A_node_without_output_ports_keeps_the_status_channel() =>
        Assert.Equal(["SubmitStatus"], Typed(new LlmParameters()).ToolPaths([]).Select(path => path.Value));

    private static LlmNodeType Typed(LlmParameters parameters) => Assert.IsType<LlmNodeType>(LlmNodeTypes.Create(parameters));

    private static RunMaterials Materials() => new()
    {
        Steps = ["本次只负责条目 1：甲", "这是第 2 轮实施。"],
        Briefing = "本轮接口",
    };
}