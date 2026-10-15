using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Node.Llm.Tests;

/// <summary>Llm 节点类型：类型名与注册、静态形状与类型规则。</summary>
public sealed class LlmNodeTypeTests
{
    private static readonly LlmNodeType NodeType = new();

    [Fact]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Llm", LlmNodeTypes.Name.Value);
        Assert.Equal(LlmNodeTypes.Name, NodeType.Kind);
    }

    [Fact]
    public void Registration_adds_the_type_to_a_registry()
    {
        NodeTypeRegistry registry = new();
        LlmNodeTypes.RegisterInto(registry);

        Assert.Equal(LlmNodeTypes.Name, registry.Find("llm")?.Kind);
    }

    [Fact]
    public void Shape_dispatches_runs()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.True(shape.DispatchesRuns);
        Assert.False(shape.Organizes);
        Assert.Equal([PortType.Text, PortType.Context, PortType.Signal], shape.InputTypes);
        Assert.Equal([PortType.Text, PortType.WorkItems, PortType.Context], shape.OutputTypes);
    }

    [Fact]
    public void Max_runs_below_one_is_rejected()
    {
        List<string> errors = [];
        NodeType.Validate(Node(new ExecutableSpec { MaxRuns = 0 }), NodeScope.Flow("测试"), error => errors.Add(error.Description));

        Assert.Equal(["流程 测试 的节点 干活：MaxRuns 必须是正整数。"], errors);
    }

    private static NodeSpec Node(ExecutableSpec execution) => new()
    {
        Name = new NodeName("干活"),
        Kind = LlmNodeTypes.Name,
        Outputs = [new Port(new PortName("结论"), PortType.Text)],
        Execution = execution,
    };
}