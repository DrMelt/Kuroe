using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.StaticGraph.Ports;

/// <summary>端口与上游连接的展示名：翻转条件附在来源后。</summary>
public sealed class PortLabelsTests
{
    private static readonly PortName Result = new("结论");

    [Fact]
    public void Negative_source_appends_the_flip_marker()
    {
        FromLink link = new(
            Result,
            PortRef.Named(new NodeLabel("来源"), Result),
            TriggerCondition.Negative);

        Assert.Equal("来源@结论·翻转", link.SourceLabel());
    }

    [Fact]
    public void Positive_source_shows_only_the_reference()
    {
        FromLink link = new(Result, PortRef.Named(new NodeLabel("来源"), Result));

        Assert.Equal("来源@结论", link.SourceLabel());
    }

    [Fact]
    public void Start_label_shows_the_default_value()
    {
        Port port = new(
            new PortName("分支"),
            PortTypes.Text,
            mode: PortMode.Constant,
            defaultValue: "main");

        Assert.Equal("分支，默认 main·常量", port.StartLabel());
    }
}