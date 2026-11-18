using Xunit;

namespace Kuroe.Tasks.Shared.Tests;

/// <summary>流程名的解析：去空白与空值拒绝。</summary>
public sealed class FlowNameTests
{
    [Fact(DisplayName = "两侧空白被去掉")]
    public void Surrounding_spaces_are_trimmed() =>
        Assert.Equal(new FlowName("默认"), FlowName.Create("  默认 ").Value);

    [Fact(DisplayName = "空白名称被拒绝")]
    public void Blank_name_is_rejected() =>
        Assert.Equal(
            "流程名不能为空。",
            FlowName.Create("  ").ErrorsOrEmptyList.Single().Description);
}
