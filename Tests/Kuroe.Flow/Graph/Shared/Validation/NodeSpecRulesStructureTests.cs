using Kuroe.Flow.Graph.Shared.StaticGraph.Validation;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Graph.Shared.StaticGraph.Ports;
using Kuroe.Flow.Graph.Shared.Tests.Support;
using Xunit;

namespace Kuroe.Flow.Graph.Shared.Tests.Validation;

/// <summary>节点结构规则：容器与执行节点的结构互斥。</summary>
public sealed class NodeSpecRulesStructureTests
{
    [Fact(DisplayName = "容器形状要求子节点")]
    public void Container_shape_requires_children()
    {
        NodeSpec node = Container();

        Assert.Equal(["容器节点必须声明子节点。"], Errors(node, NodeRuleContext.FlowDefinition));
    }

    [Fact(DisplayName = "容器不能声明执行配置")]
    public void Container_cannot_declare_execution_config()
    {
        NodeSpec node = Container() with
        {
            Nodes = [Member()],
            Spec = new ExecutableSpec(),
        };

        Assert.Equal(["容器不能同时声明执行配置。"], Errors(node, NodeRuleContext.FlowDefinition));
    }

    [Fact(DisplayName = "执行节点不能声明子节点")]
    public void Execution_node_cannot_declare_children()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = Executable(),
            Nodes = [Member()],
        };

        Assert.Equal(
            ["Executable 节点不能声明子节点，子节点只属于容器。"],
            Errors(node, NodeRuleContext.FlowDefinition));
    }

    [Fact(DisplayName = "库执行节点要求执行配置")]
    public void Library_execution_node_requires_execution_config()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = Executable(),
        };

        Assert.Equal(["节点必须声明执行配置。"], Errors(node, NodeRuleContext.LibraryDefinition));
    }

    [Fact(DisplayName = "流程执行节点可以不写配置")]
    public void Flow_execution_node_without_config_is_allowed()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = Executable(),
        };

        Assert.Empty(Errors(node, NodeRuleContext.FlowDefinition));
    }

    private static NodeSpec Container() => NodeSpec.Define(new NodeName("组")) with
    {
        Type = new TestNodeType(new NodeKind("Container"), new NodeTypeShape { Organizes = true }),
    };

    private static NodeSpec Member() => NodeSpec.Define(new NodeName("成员")) with
    {
        Type = Executable(),
        Spec = new ExecutableSpec(),
    };

    private static TestNodeType Executable() => new(new NodeKind("Executable"), new NodeTypeShape
    {
        DispatchesExecutions = true,
        InputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
        OutputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
    });

    private static List<string> Errors(NodeSpec node, NodeRuleContext context)
    {
        List<string> errors = [];
        node.CheckStructure(node.Type?.Shape, context, errors.Add);

        return errors;
    }
}