using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>节点接线与模型槽位规则：各作用域允许声明的字段。</summary>
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
    public void Container_cannot_declare_a_model()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            Model = new ModelRef("执行者"),
        };

        Assert.Equal(
            ["容器节点不是执行节点，不支持模型选择。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.FlowDefinition, fail)));
    }

    [Fact]
    public void Inline_container_cannot_declare_model_slots()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            Nodes = [NodeSpec.Define(new NodeName("成员"))],
            ModelSlots = [new ModelRef("执行者")],
        };

        Assert.Equal(
            ["内联容器不声明模型槽位与模型绑定，槽位声明只属于节点库容器定义。"],
            Errors(fail => node.CheckWiring(NodeRuleContext.FlowDefinition, fail)));
    }

    [Fact]
    public void Model_slots_must_be_named_and_unique()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("组")) with
        {
            ModelSlots = [new ModelRef(""), new ModelRef("执行者"), new ModelRef("执行者")],
        };

        Assert.Equal(["模型槽位名不能为空。", "模型槽位 执行者 重复。"], Errors(node.CheckModelSlots));
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