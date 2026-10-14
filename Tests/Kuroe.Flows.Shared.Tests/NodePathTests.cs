using Kuroe.Flows.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flows.Shared.Tests;

/// <summary>节点路径值对象：段序列相等即相等。</summary>
public sealed class NodePathTests
{
    [Fact]
    public void Paths_with_same_segments_are_equal_regardless_of_collection_instance()
    {
        NodePath first = new([new NodeName("整体"), new NodeName("文档组"), new NodeName("撰写")]);
        NodePath second = new([new NodeName("整体"), new NodeName("文档组"), new NodeName("撰写")]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Paths_with_different_segments_are_not_equal()
    {
        NodePath shallow = new([new NodeName("整体")]);
        NodePath deep = new([new NodeName("整体"), new NodeName("文档组")]);

        Assert.NotEqual(shallow, deep);
    }
}
