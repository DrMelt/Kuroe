using Kuroe.Flow.Tasks;
using Xunit;

namespace Kuroe.Flow.Tasks.Tests;

/// <summary>端口名的解析：入口校验。</summary>
public sealed class PortNameTests
{
    [Theory]
    [InlineData("  ")]
    [InlineData("结论@甲")]
    public void Invalid_port_name_is_rejected(string name) =>
        Assert.Equal(
            "端口名不能为空或含 @。",
            PortName.Create(name).ErrorsOrEmptyList.Single().Description);
}
