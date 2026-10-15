using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>节点类型名：身份由类型名文本给出，文本即流程文件里写的 Kind 取值。</summary>
public sealed class NodeTypeNameTests
{
    [Fact]
    public void Text_form_is_the_value() =>
        Assert.Equal("Input", new NodeTypeName("Input").ToString());

    [Fact]
    public void Builtin_names_are_the_kinds_written_in_files()
    {
        Assert.Equal("Container", NodeTypes.Container.Value);
        Assert.Equal("Executable", NodeTypes.Executable.Value);
    }
}
