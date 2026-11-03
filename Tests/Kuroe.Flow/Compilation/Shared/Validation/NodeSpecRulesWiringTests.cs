using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Compilation.Shared.Tests.Validation;

/// <summary>节点接线规则：各作用域允许声明的 From 与输入绑定字段。</summary>
public sealed class NodeSpecRulesWiringTests
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
    public void Reference_execution_node_bindings_are_resolved_after_expansion()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("实施"), new NodeName("库实施")) with
        {
            Bindings = Bindings(),
        };

        Assert.Empty(Errors(fail => node.CheckWiring(NodeRuleContext.Reference, fail)));
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
    public void Flow_execution_node_keeps_dynamic_wiring_in_from()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, mode: PortMode.Constant)],
            Bindings = Bindings(),
        };

        Assert.Empty(Errors(fail => node.CheckWiring(NodeRuleContext.FlowDefinition, fail)));
    }

    [Fact]
    public void Container_bindings_are_declared_as_input_bindings()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Inputs = [new InputPort(Input)],
            Bindings = Bindings(),
        };

        Assert.Empty(Errors(fail => node.CheckWiring(NodeRuleContext.FlowDefinition, fail)));
    }

    [Fact]
    public void Library_container_definition_cannot_declare_bindings()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Bindings = Bindings(),
        };

        Assert.Equal(
            ["库容器定义不接线，不能声明输入绑定。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.LibraryDefinition, fail)));
    }

    [Fact]
    public void Library_execution_definition_cannot_declare_bindings()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Spec = new ExecutableSpec(),
            Inputs = [new InputPort(Input, mode: PortMode.Constant)],
            Bindings = Bindings(),
        };

        Assert.Equal(
            ["执行节点库定义的端口由引用处绑定，不能写输入绑定。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.LibraryDefinition, fail)));
    }

    private static FromGroup Group() =>
        new([new FromLink(Input, PortRef.Named(new NodeLabel("来源"), Output))]);

    private static Dictionary<PortName, PortRef> Bindings() =>
        new() { [Input] = PortRef.Named(new NodeLabel("来源"), Output) };

    private static List<string> Errors(Action<ValidationMessageHandler> check)
    {
        List<string> errors = [];
        check(errors.Add);

        return errors;
    }
}