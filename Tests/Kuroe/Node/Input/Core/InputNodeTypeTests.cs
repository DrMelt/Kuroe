using Kuroe.FlowDescription.Description.Shared.Reporting;
using ErrorOr;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Node.Input.Tests;

/// <summary>输入节点类型的形状与类型规则：启动执行后等待回答，只以用户回答为产出。</summary>
public sealed class InputNodeTypeTests
{
    private static readonly InputNodeType NodeType = new(null);

    [Fact(DisplayName = "输入节点名称是文件书写的种类")]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Input", InputNodeTypes.Name.Value);
        Assert.Equal(InputNodeTypes.Name, NodeType.Kind);
    }

    [Fact(DisplayName = "输入类型创建把声明保留在实例上")]
    public void Create_keeps_the_declaration_on_the_instance()
    {
        INodeType type = InputNodeTypes.Create(new InputParameters { Question = "要做什么" });

        Assert.Equal(InputNodeTypes.Name, type.Kind);
        Assert.Equal("要做什么", Assert.IsType<InputNodeType>(type).Parameters?.Question);
    }

    [Fact(DisplayName = "形状派发执行并等待输入")]
    public void Shape_dispatches_execution_and_waits_for_input()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.True(shape.DispatchesExecutions);
        Assert.True(shape.AwaitsExternalInput);
        Assert.False(shape.Organizes);
        Assert.IsType<OutputDeclaration.Declared>(shape.Outputs);
        Assert.True(shape.StartsUnaided);
        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.InputTypes);
        Assert.Equal([PortTypes.Text], shape.OutputTypes);
    }

    [Fact(DisplayName = "普通等待节点通过")]
    public void Plain_waiting_node_passes() =>
        Assert.Empty(Failures(Node()));

    [Fact(DisplayName = "多于一个输出端口被拒绝")]
    public void More_than_one_output_port_is_rejected()
    {
        NodeSpec node = Node() with
        {
            Outputs =
            [
                new Port(new PortName("甲"), PortTypes.Text),
                new Port(new PortName("乙"), PortTypes.Text),
            ],
        };

        Assert.Equal(["流程 测试 的节点 收话：输入节点最多声明一个输出端口，回答写入该端口。"], Failures(node));
    }

    private static IReadOnlyList<string> Failures(NodeSpec node)
    {
        List<Error> errors = [];
        NodeType.Validate(node, NodeScope.Flow("测试"), errors.Add);

        return [.. errors.Select(error => error.Description)];
    }

    private static NodeSpec Node(ExecutableSpec? spec = null) => new()
    {
        Label = new NodeLabel("收话"),
        Type = InputNodeTypes.Create(null),
        Spec = spec ?? new ExecutableSpec(),
    };


}
