using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Graph.Shared.Tests.RuntimeGraph.Nodes;

/// <summary>一次发布：版本、触发位与内容同属一份产出，读取按端口进行。</summary>
public sealed class PublicationTests
{
    [Fact(DisplayName = "没有触发条目的端口照常发布并保留记录的 false")]
    public void Ports_without_a_trigger_entry_fire_and_recorded_false_is_kept()
    {
        Publication publication = new(
            1,
            new Dictionary<PortName, bool> { [new PortName("停")] = false },
            new Dictionary<PortName, IReadOnlyList<ContextValue>>());

        Assert.False(publication.Fires(new PortName("停")));
        Assert.True(publication.Fires(new PortName("走")));
    }

    [Fact(DisplayName = "内容按端口读取缺失端口为空")]
    public void Content_is_read_per_port_and_missing_ports_are_empty()
    {
        PortName port = new("结论");
        Publication publication = new(
            1,
            new Dictionary<PortName, bool>(),
            new Dictionary<PortName, IReadOnlyList<ContextValue>>
            {
                [port] = [new ContextValue(new StartSource(port), "内容")],
            });

        Assert.Equal("内容", Assert.Single(publication.Output(port)).Text);
        Assert.Empty(publication.Output(new PortName("另一端口")));
        Assert.Equal("内容", publication.Text(port));
        Assert.Null(publication.Text(new PortName("另一端口")));
    }

    [Fact(DisplayName = "文本按来源顺序连接多个值")]
    public void Text_joins_multiple_values_in_source_order()
    {
        PortName port = new("上下文");
        Publication publication = new(
            1,
            new Dictionary<PortName, bool>(),
            new Dictionary<PortName, IReadOnlyList<ContextValue>>
            {
                [port] =
                [
                    new ContextValue(new StartSource(port), "甲"),
                    new ContextValue(new StartSource(port), "乙"),
                ],
            });

        Assert.Equal("甲\n\n乙", publication.Text(port));
    }
}
