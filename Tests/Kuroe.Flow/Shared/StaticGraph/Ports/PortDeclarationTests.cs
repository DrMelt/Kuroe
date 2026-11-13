using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.StaticGraph.Ports;

/// <summary>端口表与端口引用：按名查找、展示文本与执行节点的端口判定。</summary>
public sealed class PortDeclarationTests
{
    [Fact]
    public void Port_table_finds_declarations_by_name()
    {
        IReadOnlyList<Port> outputs = [new Port(new PortName("结论"), PortTypes.Text), new Port(new PortName("上下文"), PortTypes.Context)];
        IReadOnlyList<InputPort> inputs = [new InputPort(new PortName("目标")), new InputPort(new PortName("催办"), PortTypes.Signal)];

        Assert.Equal(PortTypes.Context, Assert.NotNull(outputs.Find(new PortName("上下文"))).Type);
        Assert.Equal(PortTypes.Signal, Assert.NotNull(inputs.Find(new PortName("催办"))).Type);
        Assert.Null(outputs.Find(new PortName("检查")));
        Assert.Null(inputs.Find(new PortName("检查")));
    }

    [Fact]
    public void Port_renders_its_name_as_text()
    {
        Assert.Equal("结论", new Port(new PortName("结论"), PortTypes.Text).ToString());
        Assert.Equal("目标", new InputPort(new PortName("目标")).ToString());
    }

    [Fact]
    public void Port_ref_displays_bound_and_named_forms()
    {
        PortRef bound = PortRef.Bound(new PortName("目标"));
        PortRef named = PortRef.Named(new NodeLabel("分析"), new PortName("结论"));

        Assert.Equal("@目标", bound.Display);
        Assert.Null(bound.Source);
        Assert.Equal("分析@结论", named.Display);
        Assert.Equal(new NodeLabel("分析"), named.Source);
    }

    [Fact]
    public void Executable_reports_its_declared_port_kinds()
    {
        ExecutableNode executable = Executable([new Port(new PortName("结论"), PortTypes.Text)]);

        Assert.Equal([new PortName("结论")], executable.RequiredPorts.Select(port => port.Name));
        Assert.Equal(PortTypes.Text, Assert.NotNull(executable.FindPort(new PortName("结论"))).Type);
        Assert.Null(executable.FindPort(new PortName("任务")));
        Assert.Equal(100, ExecutableNode.DefaultMaxExecutions);
    }

    [Fact]
    public void Executable_keeps_declared_input_defaults()
    {
        InputPort input = new(new PortName("背景"), PortTypes.Text, null, "固定背景");
        ExecutableNode executable = Executable([]) with { Inputs = [input] };

        Assert.Equal("固定背景", Assert.Single(executable.Inputs).Default);
    }

    [Fact]
    public void Executable_without_ports_to_submit_has_no_model_output()
    {
        ExecutableNode executable = Executable([new Port(new PortName("上下文"), PortTypes.Context)]);

        Assert.Empty(executable.RequiredPorts);
    }

    [Fact]
    public void Signal_port_is_submitted_without_content()
    {
        ExecutableNode executable = Executable([new Port(new PortName("完成"), PortTypes.Signal)]);

        Assert.Equal([new PortName("完成")], executable.RequiredPorts.Select(port => port.Name));
    }

    [Fact]
    public void Start_ports_expose_outputs()
    {
        IReadOnlyList<Port> ports = [new Port(new PortName("目标"), PortTypes.Text)];

        Assert.Equal(PortTypes.Text, Assert.NotNull(ports.Find(new PortName("目标"))).Type);
        Assert.Null(ports.Find(new PortName("任务")));
    }

    private static ExecutableNode Executable(IReadOnlyList<Port> outputs) => new()
    {
        Path = NodePath.Of("实施"),
        Type = new StubNodeType(new NodeKind("Executable")),
        Spec = new ExecutableSpec(),
        Inputs = [],
        Outputs = outputs,
        MaxExecutions = ExecutableNode.DefaultMaxExecutions,
    };
}
