using Kuroe.Flow.Graph.Shared.StaticGraph.Validation;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;
using Kuroe.Executor.Shared.Ports;
using Kuroe.Flow.Graph.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Graph.Shared.Tests.Validation;

/// <summary>节点接线规则：各作用域允许声明的 From 与输入绑定字段。</summary>
public sealed class NodeSpecRulesWiringTests
{
    private static readonly PortName Input = new("输入");
    private static readonly PortName Output = new("输出");

    [Fact(DisplayName = "引用容器保留来源用于输入接线")]
    public void Reference_container_keeps_from_for_input_wiring()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("组"), new NodeName("库组")) with
        {
            From = [Group()],
        };

        Assert.Empty(Errors(fail => node.CheckWiring(NodeRuleContext.Reference, fail, container: true)));
    }

    [Fact(DisplayName = "引用执行节点绑定在展开后解析")]
    public void Reference_execution_node_bindings_are_resolved_after_expansion()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("实施"), new NodeName("库实施")) with
        {
            Bindings = Bindings(),
        };

        Assert.Empty(Errors(fail => node.CheckWiring(NodeRuleContext.Reference, fail)));
    }

    [Fact(DisplayName = "库执行定义不能声明来源")]
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

    [Fact(DisplayName = "流程执行节点在来源中保留动态接线")]
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

    [Fact(DisplayName = "容器绑定按静态输入绑定声明")]
    public void Container_bindings_are_declared_as_static_input_bindings()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Inputs = [new InputPort(Input, mode: PortMode.Constant)],
            Bindings = Bindings(),
        };

        Assert.Empty(Errors(fail => node.CheckWiring(NodeRuleContext.FlowDefinition, fail)));
    }

    [Fact(DisplayName = "库容器定义不能声明绑定")]
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

    [Fact(DisplayName = "库执行定义不能声明绑定")]
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
