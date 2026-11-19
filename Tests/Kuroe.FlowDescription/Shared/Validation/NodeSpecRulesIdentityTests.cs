using Kuroe.FlowDescription.Shared.StaticGraph.Validation;
using Kuroe.FlowDescription.Shared.Model;
using Kuroe.FlowDescription.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.FlowDescription.Shared.Tests.Validation;

/// <summary>节点标识规则：路径分隔符与定义标签。</summary>
public sealed class NodeSpecRulesIdentityTests
{
    [Fact(DisplayName = "开始可作为定义名")]
    public void Start_name_is_allowed_as_a_definition_name()
    {
        NodeSpec node = NodeSpec.Define(new NodeName("Start"));

        Assert.Empty(Errors(node, NodeRuleContext.LibraryDefinition));
    }

    [Fact(DisplayName = "引用上可以使用开始标签")]
    public void Start_label_is_allowed_on_a_reference()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("Start"), new NodeName("定义"));

        Assert.Empty(Errors(node, NodeRuleContext.Reference));
    }

    [Fact(DisplayName = "端口分隔符被拒绝")]
    public void Port_separator_is_rejected()
    {
        NodeSpec node = NodeSpec.Reference(new NodeLabel("甲@乙"), new NodeName("定义"));

        Assert.Contains("@", Errors(node, NodeRuleContext.Reference).Single());
    }

    [Fact(DisplayName = "定义标签必须与节点名一致")]
    public void Definition_label_must_match_the_node_name()
    {
        NodeSpec node = new() { Name = new NodeName("甲"), Label = new NodeLabel("乙") };

        Assert.Contains("不能另写标识", Errors(node, NodeRuleContext.FlowDefinition).Single());
    }

    [Fact(DisplayName = "合法定义没有身份错误")]
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