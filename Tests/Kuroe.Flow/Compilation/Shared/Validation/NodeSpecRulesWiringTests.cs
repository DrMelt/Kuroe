using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Compilation.Shared.Tests.Validation;

/// <summary>节点接线规则：各作用域允许声明的 From、In 与 Static 字段。</summary>
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