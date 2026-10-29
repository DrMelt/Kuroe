using Kuroe.Flow.Shared.Model;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>节点类型名：身份由类型名文本给出，文本即流程文件里写的 Kind 取值。</summary>
public sealed class NodeKindTests
{
    [Fact]
    public void Text_form_is_the_value() =>
        Assert.Equal("Input", new NodeKind("Input").ToString());
}
