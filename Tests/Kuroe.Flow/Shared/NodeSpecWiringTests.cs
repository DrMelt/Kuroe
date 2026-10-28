using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>节点接线与静态端口规则：各作用域允许声明的字段。</summary>
public sealed class NodeSpecWiringTests
{
    private static readonly PortName Input = new("输入");
    private static readonly PortName Output = new("输出");

    [Fact]
    public void Reference_container_cannot_declare_from()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("组"), new NodeName("库组")) with
        {
            From = [Group()],
        };

        Assert.Equal(
            ["引用容器不能声明 From，容器自身不接线。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.Reference, fail, container: true)));
    }

    [Fact]
    public void Reference_execution_node_cannot_declare_input_bindings()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("实施"), new NodeName("库实施")) with
        {
            In = Bindings(),
        };

        Assert.Equal(
            ["引用执行节点不写输入端口绑定，接线一律经 From。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.Reference, fail)));
    }

    [Fact]
    public void Library_execution_definition_cannot_declare_from()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            From = [Group()],
        };

        Assert.Contains(
            "执行节点库定义的接线由引用处提供",
            Errors(fail => node.CheckWiring(NodeRuleContext.LibraryDefinition, fail)).Single());
    }

    [Fact]
    public void Flow_execution_node_cannot_declare_input_bindings()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            In = Bindings(),
        };

        Assert.Equal(
            ["执行节点不写输入端口绑定，接线一律经 From。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.FlowDefinition, fail)));
    }

    [Fact]
    public void Container_cannot_declare_static_bindings()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Static = new Dictionary<PortName, PortRef>
            {
                [Input] = PortRef.Named(new NodeLabel("来源"), Output),
            },
        };

        Assert.Equal(
            ["容器节点不写 Static，静态输入由 In 绑定。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.FlowDefinition, fail)));
    }

    [Fact]
    public void Library_execution_definition_cannot_declare_static_bindings()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Static = new Dictionary<PortName, PortRef>
            {
                [Input] = PortRef.Named(new NodeLabel("来源"), Output),
            },
        };

        Assert.Equal(
            ["执行节点库定义的静态端口由引用处绑定，不能写 Static。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.LibraryDefinition, fail)));
    }

    [Fact]
    public void Static_binding_target_must_be_a_static_input()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input)],
            Static = new Dictionary<PortName, PortRef>
            {
                [Input] = PortRef.Named(new NodeLabel("来源"), Output),
            },
        };

        Assert.Equal(
            ["静态绑定目标 输入 不是已声明的静态输入端口。"],
            Errors(node.CheckStatic));
    }

    [Fact]
    public void Static_input_requires_a_binding_or_default()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, isStatic: true)],
        };

        Assert.Equal(
            ["静态输入端口 输入 没有绑定来源或默认值。"],
            Errors(node.CheckStaticCoverage));
    }

    [Fact]
    public void Static_input_accepts_a_default()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, isStatic: true, defaultValue: "固定值")],
        };

        Assert.Empty(Errors(node.CheckStaticCoverage));
    }

    [Fact]
    public void Container_static_input_cannot_declare_a_default()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Type = new StubNodeType(new NodeKind("Container"), new NodeTypeShape
            {
                Organizes = true,
                InputTypes = [PortTypes.Text],
                StaticInputs = [new InputPort(Input, PortTypes.Text, isStatic: true)],
            }),
            Inputs = [new InputPort(Input, PortTypes.Text, isStatic: true, defaultValue: "固定值")],
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
        };

        Assert.Equal(
            ["静态输入端口 输入 在容器上不能声明默认值，值由 In 绑定提供。"],
            Errors(fail => node.CheckInterface(node.Type?.Shape, fail)));
    }

    [Fact]
    public void Type_static_input_cannot_be_declared_dynamic()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = new StubNodeType(new NodeKind("Executable"), new NodeTypeShape
            {
                DispatchesExecutions = true,
                InputTypes = [PortTypes.Text],
                OutputTypes = [PortTypes.Text],
                StaticInputs = [new InputPort(Input, PortTypes.Text, isStatic: true)],
            }),
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, PortTypes.Text)],
        };

        Assert.Equal(
            ["输入端口 输入 是节点类型的静态端口，不能声明为动态端口。"],
            Errors(fail => node.CheckInterface(node.Type?.Shape, fail)));
    }

    [Fact]
    public void Static_input_must_be_text()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = new StubNodeType(new NodeKind("Executable"), new NodeTypeShape
            {
                DispatchesExecutions = true,
                InputTypes = [PortTypes.Text, PortTypes.Signal],
                OutputTypes = [PortTypes.Text],
            }),
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, PortTypes.Signal, isStatic: true)],
        };

        Assert.Equal(
            ["静态输入端口 输入 的类型只能是 Text。"],
            Errors(fail => node.CheckInterface(node.Type?.Shape, fail)));
    }

    private static FromGroup Group() =>
        new([new FromLink(Input, PortRef.Named(new NodeLabel("来源"), Output))]);

    private static Dictionary<PortName, PortRef> Bindings() =>
        new() { [Input] = PortRef.Named(new NodeLabel("来源"), Output) };

    private static List<string> Errors(Action<Action<string>> check)
    {
        List<string> errors = [];
        check(errors.Add);

        return errors;
    }
}