using Xunit;

namespace Kuroe.Host.Ability.Node.Container.Tests;

/// <summary>引擎内置的容器类型：类型名即流程文件里写的 Kind 取值。</summary>
public sealed class ContainerNodeTypesTests
{
    [Fact(DisplayName = "内置名称就是文件书写的种类")]
    public void Builtin_name_is_the_kind_written_in_files() =>
        Assert.Equal("Container", ContainerNodeTypes.Name.Value);
}