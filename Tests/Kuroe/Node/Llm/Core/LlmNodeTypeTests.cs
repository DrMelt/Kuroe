using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>Llm 节点类型：类型名与注册、静态形状与类型规则。</summary>
public sealed class LlmNodeTypeTests
{
    private static readonly LlmNodeType NodeType = new(null);

    [Fact]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Llm", LlmNodeTypes.Name.Value);
        Assert.Equal(LlmNodeTypes.Name, NodeType.Kind);
    }

    [Fact]
    public void Create_keeps_the_declaration_on_the_instance()
    {
        INodeType type = LlmNodeTypes.Create(new LlmParameters { Prompt = "拆一下" });

        Assert.Equal(LlmNodeTypes.Name, type.Kind);
        Assert.Equal("拆一下", Assert.IsType<LlmNodeType>(type).Parameters?.Prompt);
    }

    [Fact]
    public void Shape_dispatches_executions()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.True(shape.DispatchesExecutions);
        Assert.False(shape.Organizes);
        Assert.Equal([PortTypes.Text, PortTypes.TextTrigger, PortTypes.Context, PortTypes.Signal, PortTypes.SignalTrigger], shape.InputTypes);
        Assert.Equal([PortTypes.Text, PortTypes.TextTrigger, PortTypes.Context, PortTypes.Signal, PortTypes.SignalTrigger], shape.OutputTypes);
    }

    [Fact]
    public void Max_executions_below_one_is_rejected()
    {
        List<string> errors = [];
        NodeType.Validate(Node(new ExecutableSpec { MaxExecutions = 0 }), NodeScope.Flow("测试"), error => errors.Add(error.Description));

        Assert.Equal(["流程 测试 的节点 干活：MaxExecutions 必须是正整数。"], errors);
    }

    private static NodeSpec Node(ExecutableSpec spec) => new()
    {
        Label = new NodeLabel("干活"),
        Type = LlmNodeTypes.Create(null),
        Outputs = [new Port(new PortName("结论"), PortTypes.Text)],
        Spec = spec,
    };
}