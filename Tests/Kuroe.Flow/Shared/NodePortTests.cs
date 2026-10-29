using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>端口表与端口引用：按名查找、展示文本与执行节点的端口判定。</summary>
public sealed class NodePortTests
{
    [Fact]
    public void Port_table_finds_declarations_by_name()
    {
        IReadOnlyList<Port> outputs = [new Port(new PortName("结论"), PortTypes.Text), new Port(new PortName("上下文"), PortTypes.Context)];
        IReadOnlyList<InputPort> inputs = [new InputPort(new PortName("目标")), new InputPort(new PortName("催办"), PortTypes.Signal)];

        Assert.Equal(PortTypes.Context, outputs.Find(new PortName("上下文"))!.Value.Type);
        Assert.Equal(PortTypes.Signal, inputs.Find(new PortName("催办"))!.Value.Type);
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
        Assert.Equal(PortTypes.Text, executable.FindPort(new PortName("结论"))!.Value.Type);
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
    public void Start_node_reserves_its_label_and_exposes_outputs()
    {
        StartNode start = new()
        {
            Path = new NodePath([StartNode.ReservedLabel]),
            Outputs = [new Port(new PortName("目标"), PortTypes.Text)],
        };

        Assert.Equal("Start", StartNode.ReservedLabel.Value);
        Assert.Equal(PortTypes.Text, start.FindPort(new PortName("目标"))!.Value.Type);
        Assert.Null(start.FindPort(new PortName("任务")));
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
