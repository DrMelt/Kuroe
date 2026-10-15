using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>流程名与端口名的解析：入口校验与去空白。</summary>
public sealed class FlowValueObjectTests
{
    [Theory]
    [InlineData("  ")]
    [InlineData("结论@甲")]
    public void Invalid_port_name_is_rejected(string name) =>
        Assert.Equal(
            "端口名不能为空或含 @。",
            PortName.Create(name).ErrorsOrEmptyList.Single().Description);

    [Fact]
    public void Flow_name_with_surrounding_spaces_is_trimmed() =>
        Assert.Equal(new FlowName("默认"), FlowName.Create("  默认 ").Value);

    [Fact]
    public void Blank_flow_name_is_rejected() =>
        Assert.Equal(
            "流程名不能为空。",
            FlowName.Create("  ").ErrorsOrEmptyList.Single().Description);
}
