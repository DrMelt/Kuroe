using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Compilation.Shared.Tests.Support;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Compilation.Shared.Tests.Validation;

/// <summary>节点接口规则：端口类型落在类型值域，静态端口不能声明为动态输入，默认值只用于派发执行的 Text 输入。</summary>
public sealed class NodeSpecRulesInterfaceTests
{
    private static readonly PortName Input = new("输入");

    [Fact]
    public void Input_port_type_outside_the_shape_is_rejected()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(new PortName("背景"), PortTypes.Context)],
        };

        Assert.Equal(["输入端口 背景 的类型只能是 Text。"], Errors(node, Shape(input: [PortTypes.Text])));
    }

    [Fact]
    public void Unknown_shape_skips_port_type_domain()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(new PortName("背景"), PortTypes.Context)],
        };

        Assert.Empty(Errors(node, shape: null));
    }

    [Fact]
    public void Default_value_is_rejected_on_context_input()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(new PortName("背景"), PortTypes.Context, null, "默认")],
        };

        Assert.Equal(
            ["输入端口 背景 的默认值只用于派发执行的节点的 Text 输入端口。"],
            Errors(node, Shape()));
    }

    [Fact]
    public void Container_static_input_cannot_declare_a_default()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Type = new TestNodeType(new NodeKind("Container"), new NodeTypeShape
            {
                Organizes = true,
                InputTypes = [PortTypes.Text],
            }),
            Inputs = [new InputPort(Input, PortTypes.Text, mode: PortMode.Constant, defaultValue: "固定值")],
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
        };

        Assert.Equal(
            ["静态输入端口 输入 在容器上不能声明默认值，值由引用处的绑定提供。"],
            Errors(node, node.Type?.Shape));
    }

    [Fact]
    public void Container_input_can_declare_trigger()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Type = new TestNodeType(new NodeKind("Container"), new NodeTypeShape
            {
                Organizes = true,
                InputTypes = [PortTypes.Text],
            }),
            Inputs = [new InputPort(Input, PortTypes.Text, mode: PortMode.Trigger)],
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
        };

        Assert.Empty(Errors(node, node.Type?.Shape));
    }

    [Fact]
    public void Container_constant_output_can_reference_its_static_input()
    {
        NodeSpec node = ContainerWithOutput(PortMode.Constant, Input);

        Assert.Empty(Errors(node, node.Type?.Shape));
    }

    [Fact]
    public void Container_dynamic_output_cannot_reference_its_static_input()
    {
        NodeSpec node = ContainerWithOutput(PortMode.Plain, Input);

        Assert.Equal(
            ["输出端口 结果 的常量性必须与来源 @输入 一致。"],
            Errors(node, node.Type?.Shape));
    }

    [Fact]
    public void Dynamic_output_with_value_is_rejected()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = TestNodeTypes.PlainExecutable,
            Spec = new ExecutableSpec(),
            Outputs = [new Port(new PortName("输出"), PortTypes.Text, value: "固定")],
        };

        Assert.Equal(["动态输出端口 输出 不能写 Value。"], Errors(node, node.Type?.Shape));
    }

    private static NodeSpec ContainerWithOutput(PortMode outputMode, PortName input) =>
        NodeSpec.Define(new NodeName("组")) with
        {
            Type = new TestNodeType(new NodeKind("Container"), new NodeTypeShape
            {
                Organizes = true,
                InputTypes = [PortTypes.Text],
                OutputTypes = [PortTypes.Text],
            }),
            Inputs = [new InputPort(input, PortTypes.Text, mode: PortMode.Constant)],
            Outputs = [new Port(new PortName("结果"), PortTypes.Text, reference: PortRef.Bound(input), mode: outputMode)],
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
        };

    [Fact]
    public void Static_input_accepts_non_text_types()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = new TestNodeType(new NodeKind("Executable"), new NodeTypeShape
            {
                DispatchesExecutions = true,
                InputTypes = [PortTypes.Text, PortTypes.Context],
                OutputTypes = [PortTypes.Text],
            }),
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, PortTypes.Context, mode: PortMode.Constant)],
            Outputs = [new Port(new PortName("输出"), PortTypes.Text, value: "固定", mode: PortMode.Constant)],
        };

        Assert.Empty(Errors(node, node.Type?.Shape));
    }

    [Fact]
    public void Port_mode_must_be_supported_by_its_type()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = new TestNodeType(new NodeKind("Executable"), new NodeTypeShape
            {
                DispatchesExecutions = true,
                InputTypes = [PortTypes.Context],
                OutputTypes = [PortTypes.Text],
            }),
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, PortTypes.Context, mode: PortMode.Trigger)],
        };

        Assert.Equal(
            ["输入端口 输入 的类型 Context 不支持模式 Trigger。"],
            Errors(node, node.Type?.Shape));
    }

    [Fact]
    public void Given_output_types_do_not_need_a_declared_type_range()
    {
        NodeTypeShape shape = new()
        {
            DispatchesExecutions = true,
            InputTypes = [PortTypes.Text],
            Outputs = new OutputDeclaration.Given([new Port(new PortName("输出"), PortTypes.Signal)]),
        };
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Outputs = [new Port(new PortName("输出"), PortTypes.Signal)],
        };

        Assert.Empty(Errors(node, shape));
    }

    [Fact]
    public void Given_output_ports_must_match_the_type_ports()
    {
        NodeTypeShape shape = new()
        {
            DispatchesExecutions = true,
            InputTypes = [PortTypes.Text],
            Outputs = new OutputDeclaration.Given([new Port(new PortName("输出"), PortTypes.Text)]),
        };
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Outputs = [new Port(new PortName("其他"), PortTypes.Text)],
        };

        Assert.Equal(["输出端口必须与类型给出的端口一致。"], Errors(node, shape));
    }

    [Fact]
    public void Given_output_port_name_with_at_is_rejected()
    {
        NodeTypeShape shape = new()
        {
            DispatchesExecutions = true,
            InputTypes = [PortTypes.Text],
            Outputs = new OutputDeclaration.Given([new Port(new PortName("甲@乙"), PortTypes.Text)]),
        };
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Outputs = [new Port(new PortName("甲@乙"), PortTypes.Text)],
        };

        Assert.Equal(["输出端口名 甲@乙 不能含 @。"], Errors(node, shape));
    }

    private static NodeTypeShape Shape(IReadOnlyList<PortType>? input = null) => new()
    {
        DispatchesExecutions = true,
        InputTypes = input ?? [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
        OutputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
    };

    private static List<string> Errors(NodeSpec node, NodeTypeShape? shape)
    {
        List<string> errors = [];
        node.CheckInterface(shape, errors.Add);

        return errors;
    }
}
