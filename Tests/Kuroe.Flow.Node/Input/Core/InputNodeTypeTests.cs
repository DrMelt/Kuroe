using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Node.Input.Tests;

/// <summary>输入节点类型的形状与类型规则：不启动 run，只以用户回答为产出。</summary>
public sealed class InputNodeTypeTests
{
    private static readonly StubInputChannel Channel = new();
    private static readonly InputNodeType NodeType = new(null, Channel);

    [Fact]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Input", InputNodeTypes.Name.Value);
        Assert.Equal(InputNodeTypes.Name, NodeType.Kind);
    }

    [Fact]
    public void Create_keeps_the_declaration_on_the_instance()
    {
        INodeType type = InputNodeTypes.Create(new InputParameters { Question = "要做什么" }, Channel);

        Assert.Equal(InputNodeTypes.Name, type.Kind);
        Assert.Equal("要做什么", Assert.IsType<InputNodeType>(type).Parameters?.Question);
    }

    [Fact]
    public void Shape_waits_without_dispatching_runs()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.False(shape.DispatchesRuns);
        Assert.False(shape.Organizes);
        Assert.False(shape.DerivesOutputsFromInputs);
        Assert.True(shape.StartsUnaided);
        Assert.Equal([PortType.Text, PortType.Signal], shape.InputTypes);
        Assert.Equal([PortType.Text], shape.OutputTypes);
    }

    [Fact]
    public void Plain_waiting_node_passes() =>
        Assert.Empty(Failures(Node()));

    [Fact]
    public void Execution_config_is_rejected()
    {
        NodeSpec node = Node(new ExecutableSpec
        {
            Mode = NodeMode.PerItem,
            Branch = new BranchName("甲"),
            Validate = new OutputValidation(ValidationPredicate.NonEmpty, null),
        }) with
        {
            Model = new ModelRef("规划者"),
        };

        string[] errors = [.. Failures(node)];

        Assert.Equal(4, errors.Length);
        Assert.Contains(errors, error => error.EndsWith("输入节点不启动 run，不能声明模型选择。"));
        Assert.Contains(errors, error => error.EndsWith("输入节点不启动 run，不能声明 Validate。"));
        Assert.Contains(errors, error => error.EndsWith("输入节点只能整节点等待用户输入。"));
        Assert.Contains(errors, error => error.EndsWith("输入节点不启动 run，不能声明 Branch。"));
    }

    [Fact]
    public void More_than_one_output_port_is_rejected()
    {
        NodeSpec node = Node() with
        {
            Outputs =
            [
                new Port(new PortName("甲"), PortType.Text),
                new Port(new PortName("乙"), PortType.Text),
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

    private static NodeSpec Node(ExecutableSpec? execution = null) => new()
    {
        Label = new NodeLabel("收话"),
        Type = InputNodeTypes.Create(null, Channel),
        Execution = execution ?? new ExecutableSpec(),
    };
}
