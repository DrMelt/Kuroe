using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>一次激活的输入：按输入端口给出取值、连接与触发值。</summary>
public sealed class NodeInputValuesTests
{
    private static readonly PortName Content = new("正文");
    private static readonly PortName Approval = new("批准");

    [Fact]
    public void Text_joins_values_in_binding_order()
    {
        NodeInputValues values = new(
            [
                new NodeInputValue(Content, PortTypes.Text, Value("甲")),
                new NodeInputValue(Content, PortTypes.Text, Value("乙")),
            ],
            new HashSet<PortName> { Content });

        Assert.Equal("甲\n\n乙", values.Text(Content));
        Assert.Equal(2, values.Values(Content).Count);
    }

    [Fact]
    public void Text_is_empty_when_the_port_has_no_value()
    {
        NodeInputValues values = new([], new HashSet<PortName>());

        Assert.Null(values.Text(Content));
        Assert.Empty(values.Values(Content));
        Assert.False(values.Connected(Content));
    }

    [Fact]
    public void Connected_distinguishes_ports_without_values()
    {
        NodeInputValues values = new([], new HashSet<PortName> { Approval });

        Assert.True(values.Connected(Approval));
        Assert.False(values.Connected(Content));
    }

    [Fact]
    public void Triggers_defaults_to_true_and_keeps_the_recorded_value()
    {
        NodeInputValues values = new(
            [],
            new HashSet<PortName> { Content, Approval },
            new Dictionary<PortName, bool> { [Approval] = false });

        Assert.True(values.Triggers(Content));
        Assert.False(values.Triggers(Approval));
    }

    [Fact]
    public void Static_texts_are_read_through_text_and_stay_out_of_dynamic_values()
    {
        PortName staticPort = new("静态");
        NodeInputValues values = new([], new HashSet<PortName>());

        NodeInputValues withStatic = values.WithStatic(
            new PortTexts(new Dictionary<PortName, string> { [staticPort] = "静态值" }));

        Assert.Equal("静态值", withStatic.Text(staticPort));
        Assert.Null(withStatic.Text(new PortName("动态")));
        Assert.Empty(withStatic.Values(staticPort));
        Assert.Equal("静态值", withStatic.StaticTexts.Text(staticPort));
    }

    [Fact]
    public void Empty_static_texts_keep_the_instance()
    {
        NodeInputValues values = new([], new HashSet<PortName>());

        Assert.Same(values, values.WithStatic(PortTexts.Empty));
    }

    [Fact]
    public void Text_reads_a_static_port_through_the_same_entry()
    {
        PortName port = new("路径");

        NodeInputValues values = new(
            [new NodeInputValue(port, PortTypes.Text, Value("动态值"))],
            new HashSet<PortName> { port },
            staticTexts: new PortTexts(new Dictionary<PortName, string> { [port] = "静态值" }));

        Assert.Equal("静态值", values.Text(port));
    }

    private static ContextValue Value(string text) => new(new StartSource(Content), text);
}