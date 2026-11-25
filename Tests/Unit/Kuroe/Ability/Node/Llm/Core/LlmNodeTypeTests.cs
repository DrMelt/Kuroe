using Kuroe.FlowDescription.Description.Shared.Reporting;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Ability.Node.Llm.Tests;

/// <summary>Llm 节点类型：类型名与注册、静态形状与类型规则。</summary>
public sealed class LlmNodeTypeTests
{
    private static readonly LlmNodeType NodeType = new(null);

    [Fact(DisplayName = "LLM 节点名称是文件书写的种类")]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Llm", LlmNodeTypes.Name.Value);
        Assert.Equal(LlmNodeTypes.Name, NodeType.Kind);
    }

    [Fact(DisplayName = "LLM 类型创建把声明保留在实例上")]
    public void Create_keeps_the_declaration_on_the_instance()
    {
        INodeType type = LlmNodeTypes.Create(new LlmParameters { Prompt = "拆一下" });

        Assert.Equal(LlmNodeTypes.Name, type.Kind);
        Assert.Equal("拆一下", Assert.IsType<LlmNodeType>(type).Parameters?.Prompt);
    }

    [Fact(DisplayName = "形状派发执行")]
    public void Shape_dispatches_executions()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.True(shape.DispatchesExecutions);
        Assert.False(shape.Organizes);
        Assert.Equal([PortTypes.Text, PortTypes.Context, PortTypes.Signal], shape.InputTypes);
        Assert.Equal([PortTypes.Text, PortTypes.Context, PortTypes.Signal], shape.OutputTypes);
    }

    [Fact(DisplayName = "执行上限小于一被拒绝")]
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
