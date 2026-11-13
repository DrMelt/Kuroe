using Kuroe.Flow.Node.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Tasks;

/// <summary>开始输入规则：提交只覆盖承载内容的开始端口，非内容端口不进入提交。</summary>
public sealed class StartInputRulesTests
{
    [Fact]
    public void Text_start_port_requires_input_without_default()
    {
        Port port = new(new PortName("目标"), PortTypes.Text);

        Assert.Equal(StartInputKind.Input, StartInputRules.Kind(port, hostBound: false));
        Assert.True(StartInputRules.AcceptsInput(port, hostBound: false));
        Assert.True(StartInputRules.RequiresInput(port, hostBound: false));
    }

    [Fact]
    public void Text_start_port_with_default_is_optional()
    {
        Port port = new(new PortName("分支"), PortTypes.Text, defaultValue: "main");

        Assert.Equal(StartInputKind.Input, StartInputRules.Kind(port, hostBound: false));
        Assert.True(StartInputRules.AcceptsInput(port, hostBound: false));
        Assert.False(StartInputRules.RequiresInput(port, hostBound: false));
    }

    [Fact]
    public void Signal_start_port_never_accepts_input()
    {
        Port signal = new(new PortName("启动"), PortTypes.Signal);

        Assert.Equal(StartInputKind.Automatic, StartInputRules.Kind(signal, hostBound: false));
        Assert.False(StartInputRules.AcceptsInput(signal, hostBound: false));
        Assert.False(StartInputRules.RequiresInput(signal, hostBound: false));
    }

    [Fact]
    public void Context_start_port_accepts_input()
    {
        Port context = new(new PortName("背景"), PortTypes.Context, mode: PortMode.Constant);
        Port fixedContext = new(new PortName("固定背景"), PortTypes.Context, mode: PortMode.Constant, value: "固定背景");

        Assert.Equal(StartInputKind.Input, StartInputRules.Kind(context, hostBound: false));
        Assert.True(StartInputRules.AcceptsInput(context, hostBound: false));
        Assert.True(StartInputRules.RequiresInput(context, hostBound: false));
        Assert.Equal(StartInputKind.Fixed, StartInputRules.Kind(fixedContext, hostBound: false));
        Assert.False(StartInputRules.AcceptsInput(fixedContext, hostBound: false));
    }

    [Fact]
    public void Fixed_and_host_bound_ports_do_not_accept_input()
    {
        Port fixedPort = new(new PortName("分支"), PortTypes.Text, value: "main");
        Port hostPort = new(new PortName("模型"), PortTypes.Text);

        Assert.Equal(StartInputKind.Fixed, StartInputRules.Kind(fixedPort, hostBound: false));
        Assert.Equal(StartInputKind.Host, StartInputRules.Kind(hostPort, hostBound: true));
        Assert.False(StartInputRules.AcceptsInput(fixedPort, hostBound: false));
        Assert.False(StartInputRules.AcceptsInput(hostPort, hostBound: true));
    }

    /// <summary>固定值优先于宿主绑定，宿主绑定优先于承载内容。</summary>
    [Fact]
    public void Kind_uses_fixed_then_host_then_content()
    {
        Port both = new(new PortName("模型"), PortTypes.Text, value: "固定", mode: PortMode.Constant);

        Assert.Equal(StartInputKind.Fixed, StartInputRules.Kind(both, hostBound: true));
        Assert.Equal(StartInputKind.Host, StartInputRules.Kind(new Port(new PortName("模型"), PortTypes.Text), hostBound: true));
    }
}