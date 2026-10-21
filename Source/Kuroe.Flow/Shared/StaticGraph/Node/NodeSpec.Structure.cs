namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>节点结构规则：容器与执行节点的结构互斥。</summary>
public sealed partial record NodeSpec
{
    /// <summary>节点是否组织子节点。结构以子节点表为准，类型形状只作声明依据。</summary>
    public bool IsContainer => Nodes is { Count: > 0 };

    /// <summary>按类型形状检查节点结构。</summary>
    public void CheckStructure(NodeTypeShape? shape, NodeRuleContext context, Action<string> fail)
    {
        if (Use is not null || shape is null)
        {
            return;
        }

        if (shape.Organizes)
        {
            if (!IsContainer)
            {
                fail("容器节点必须声明子节点。");
            }

            if (Spec is not null)
            {
                fail("容器不能同时声明执行配置。");
            }

            return;
        }

        if (IsContainer)
        {
            fail(context == NodeRuleContext.LibraryDefinition
                ? "执行节点与输入节点不能声明子节点，子节点只属于容器。"
                : $"{Kind?.Value ?? "执行"} 节点不能声明子节点，子节点只属于容器。");
        }

        if (Spec is null && context == NodeRuleContext.LibraryDefinition)
        {
            fail("节点必须声明执行配置。");
        }
    }
}