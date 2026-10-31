using Xunit;

namespace Kuroe.Flow.NodeType.Container.Tests;

/// <summary>引擎内置的容器类型：类型名即流程文件里写的 Kind 取值。</summary>
public sealed class ContainerNodeTypesTests
{
    [Fact]
    public void Builtin_name_is_the_kind_written_in_files() =>
        Assert.Equal("Container", ContainerNodeTypes.Name.Value);
}