using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.Flow.Compilation.Shared.Tests.Validation;

/// <summary>节点标识规则：保留名、路径分隔符与定义标签。</summary>
public sealed class NodeSpecRulesIdentityTests
{
    [Fact]
    public void Reserved_start_name_is_rejected()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("Start"));

        Assert.Contains("保留名", Errors(node, NodeRuleContext.LibraryDefinition).Single());
    }

    [Fact]
    public void Reserved_start_label_on_a_reference_is_rejected()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("Start"), new NodeName("定义"));

        Assert.Contains("保留标签", Errors(node, NodeRuleContext.Reference).Single());
    }

    [Fact]
    public void Port_separator_is_rejected()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("甲@乙"), new NodeName("定义"));

        Assert.Contains("@", Errors(node, NodeRuleContext.Reference).Single());
    }

    [Fact]
    public void Definition_label_must_match_the_node_name()
    {
        NodeSpec node = new() { Name = new NodeName("甲"), Label = new NodeLabel("乙") };

        Assert.Contains("不能另写标识", Errors(node, NodeRuleContext.FlowDefinition).Single());
    }

    [Fact]
    public void Valid_definition_has_no_identity_error()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("实施"));

        Assert.Empty(Errors(node, NodeRuleContext.FlowDefinition));
    }

    private static List<string> Errors(NodeSpec node, NodeRuleContext context)
    {
        List<string> errors = [];
        node.CheckIdentity(context, errors.Add);

        return errors;
    }
}