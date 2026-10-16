using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>端口表与端口引用：按名查找、展示文本与执行节点的端口判定。</summary>
public sealed class NodePortTests
{
    [Fact]
    public void Port_table_finds_declarations_by_name()
    {
        IReadOnlyList<Port> outputs = [new Port(new PortName("结论"), PortType.Text), new Port(new PortName("任务"), PortType.WorkItems)];
        IReadOnlyList<InputPort> inputs = [new InputPort(new PortName("目标")), new InputPort(new PortName("催办"), PortType.Signal)];

        Assert.Equal(PortType.WorkItems, outputs.Find(new PortName("任务"))!.Value.Type);
        Assert.Equal(PortType.Signal, inputs.Find(new PortName("催办"))!.Value.Type);
        Assert.Null(outputs.Find(new PortName("检查")));
        Assert.Null(inputs.Find(new PortName("检查")));
    }

    [Fact]
    public void Port_renders_its_name_as_text()
    {
        Assert.Equal("结论", new Port(new PortName("结论"), PortType.Text).ToString());
        Assert.Equal("目标", new InputPort(new PortName("目标")).ToString());
    }

    [Fact]
    public void Port_ref_displays_bound_and_named_forms()
    {
        PortRef bound = PortRef.Bound(new PortName("目标"));
        PortRef named = PortRef.Named(new NodeName("分析"), new PortName("结论"));

        Assert.Equal("@目标", bound.Display);
        Assert.Null(bound.Source);
        Assert.Equal("分析@结论", named.Display);
        Assert.Equal(new NodeName("分析"), named.Source);
    }

    [Fact]
    public void Executable_reports_its_declared_port_kinds()
    {
        ExecutableNode executable = Executable([new Port(new PortName("结论"), PortType.Text)]);

        Assert.True(executable.HasTextPorts);
        Assert.False(executable.HasWorkItemsPort);
        Assert.Equal(PortType.Text, executable.FindPort(new PortName("结论"))!.Value.Type);
        Assert.Null(executable.FindPort(new PortName("任务")));
        Assert.Equal(100, ExecutableNode.DefaultMaxRuns);
    }

    [Fact]
    public void Executable_without_text_ports_has_no_model_output()
    {
        ExecutableNode executable = Executable([new Port(new PortName("任务"), PortType.WorkItems)]);

        Assert.False(executable.HasTextPorts);
        Assert.True(executable.HasWorkItemsPort);
    }

    [Fact]
    public void Start_node_reserves_its_name_and_exposes_outputs()
    {
        StartNode start = new()
        {
            Index = 0,
            Name = StartNode.ReservedName,
            Path = new NodePath([StartNode.ReservedName]),
            Outputs = [new Port(new PortName("目标"), PortType.Text)],
        };

        Assert.Equal("Start", StartNode.ReservedName.Value);
        Assert.Equal(PortType.Text, start.FindPort(new PortName("目标"))!.Value.Type);
        Assert.Null(start.FindPort(new PortName("任务")));
    }

    private static ExecutableNode Executable(IReadOnlyList<Port> outputs) => new()
    {
        Index = 1,
        Name = new NodeName("实施"),
        Path = new NodePath([new NodeName("实施")]),
        Type = new StubNodeType(new NodeKind("Executable")),
        Execution = new ExecutableSpec(),
        Model = new ModelDefinition { Name = new ModelRef("执行者") },
        Outputs = outputs,
        MaxRuns = ExecutableNode.DefaultMaxRuns,
    };
}
