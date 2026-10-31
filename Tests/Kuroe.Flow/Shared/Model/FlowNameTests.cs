using Kuroe.Flow.Shared.StaticGraph.Flow;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.Model;

/// <summary>流程名的解析：去空白与空值拒绝。</summary>
public sealed class FlowNameTests
{
    [Fact]
    public void Surrounding_spaces_are_trimmed() =>
        Assert.Equal(new FlowName("默认"), FlowName.Create("  默认 ").Value);

    [Fact]
    public void Blank_name_is_rejected() =>
        Assert.Equal(
            "流程名不能为空。",
            FlowName.Create("  ").ErrorsOrEmptyList.Single().Description);
}
