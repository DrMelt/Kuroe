using Kuroe.Flow.Shared.Execution;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>本轮材料：引擎给出的执行说明与本轮接口说明，合成由类型描述承担。</summary>
public sealed class RunMaterialsTests
{
    [Fact]
    public void Materials_become_the_instruction_as_is() =>
        Assert.Equal("本次只负责条目 1：甲\n这是第 2 轮实施。", TestNodeTypes.Executable.Compose(Materials()));

    [Fact]
    public void Materials_without_steps_leave_an_empty_instruction() =>
        Assert.Empty(TestNodeTypes.Executable.Compose(new RunMaterials { Briefing = "本轮接口" }));

    [Fact]
    public void Materials_carry_the_engine_briefing() =>
        Assert.Equal("本轮接口", Materials().Briefing);

    private static RunMaterials Materials() => new()
    {
        Steps = ["本次只负责条目 1：甲", "这是第 2 轮实施。"],
        Briefing = "本轮接口",
    };
}