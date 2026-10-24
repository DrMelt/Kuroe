using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>节点结构规则：容器与执行节点的结构互斥。</summary>
public sealed class NodeSpecStructureTests
{
    [Fact]
    public void Container_shape_requires_children()
    {
        NodeSpec node = Container();

        Assert.Equal(["容器节点必须声明子节点。"], Errors(node, NodeRuleContext.FlowDefinition));
    }

    [Fact]
    public void Container_cannot_declare_execution_config()
    {
        NodeSpec node = Container() with
        {
            Nodes = [Member()],
            Spec = new ExecutableSpec(),
        };

        Assert.Equal(["容器不能同时声明执行配置。"], Errors(node, NodeRuleContext.FlowDefinition));
    }

    [Fact]
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

    [Fact]
    public void Library_execution_node_requires_execution_config()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施")) with
        {
            Type = Executable(),
        };

        Assert.Equal(["节点必须声明执行配置。"], Errors(node, NodeRuleContext.LibraryDefinition));
    }

    [Fact]
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
        Type = new TestType(new NodeKind("Container"), new NodeTypeShape { Organizes = true }),
    };

    private static NodeSpec Member() => NodeSpec.Define(new NodeName("成员")) with
    {
        Type = Executable(),
        Spec = new ExecutableSpec(),
    };

    private static TestType Executable() => new(new NodeKind("Executable"), new NodeTypeShape
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

    private sealed class TestType(NodeKind kind, NodeTypeShape shape) : INodeType
    {
        public NodeKind Kind { get; } = kind;

        public NodeTypeShape Shape { get; } = shape;

        public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
        {
        }
    }
}