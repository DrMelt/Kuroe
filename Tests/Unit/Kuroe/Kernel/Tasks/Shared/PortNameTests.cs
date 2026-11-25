using Xunit;

namespace Kuroe.Kernel.Tasks.Shared.Tests;

/// <summary>端口名的解析：入口校验。</summary>
public sealed class PortNameTests
{
    [Theory]
    [InlineData("  ", Label = "空白名称")]
    [InlineData("结论@甲", Label = "含 at 符号")]
    public void Invalid_port_name_is_rejected(string name) =>
        Assert.Equal(
            "端口名不能为空或含 @。",
            PortName.Create(name).ErrorsOrEmptyList.Single().Description);
}
