using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>本轮请求的合成：引擎材料的摆放。</summary>
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

    private static RunMaterials Materials() => new()
    {
        Steps = ["本次只负责条目 1：甲", "这是第 2 轮实施。"],
        Briefing = "本轮接口",
        ContractTools = [new CapabilityPath("SubmitStatus")],
    };
}