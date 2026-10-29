using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点结构规则：容器与执行节点的结构互斥。</summary>
public static partial class NodeSpecRules
{
    /// <summary>按类型形状检查节点结构。</summary>
    public static void CheckStructure(
        this NodeSpec node,
        NodeTypeShape? shape,
        NodeRuleContext context,
        Action<string> fail)
    {
        if (node.Use is not null || shape is null)
        {
            return;
        }

        if (shape.Organizes)
        {
            if (!node.IsContainer)
            {
                fail("容器节点必须声明子节点。");
            }

            if (node.Spec is not null)
            {
                fail("容器不能同时声明执行配置。");
            }

            return;
        }

        if (node.IsContainer)
        {
            fail(context == NodeRuleContext.LibraryDefinition
                ? "执行节点与输入节点不能声明子节点，子节点只属于容器。"
                : $"{node.Kind?.Value ?? "执行"} 节点不能声明子节点，子节点只属于容器。");
        }

        if (node.Spec is null && context == NodeRuleContext.LibraryDefinition)
        {
            fail("节点必须声明执行配置。");
        }
    }
}