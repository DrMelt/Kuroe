using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点标识规则：名称、标签与路径分隔符。</summary>
public static partial class NodeSpecRules
{
    /// <summary>按作用域检查节点标识：定义条目按节点名，引用条目按标签。</summary>
    public static void CheckIdentity(this NodeSpec node, NodeRuleContext context, ValidationMessageHandler fail)
    {
        if (context == NodeRuleContext.Reference || node.Use is not null || node.Name is null)
        {
            CheckReference(node, fail);
            return;
        }

        CheckDefinition(node, fail);
    }

    /// <summary>检查定义条目的节点名，标签必须取节点名文本。</summary>
    private static void CheckDefinition(NodeSpec node, ValidationMessageHandler fail)
    {
        if (node.Name is not { } name)
        {
            fail("节点名不能为空。");

            return;
        }

        Check(name.Value, "节点名", fail);

        if (node.Label.Value != name.Value)
        {
            fail($"定义条目的标签取节点名 {name.Value} 的文本，不能另写标识。");
        }
    }

    /// <summary>检查引用条目的标签。</summary>
    private static void CheckReference(NodeSpec node, ValidationMessageHandler fail) =>
        Check(node.Label.Value, "节点标签", fail);

    private static void Check(string value, string kind, ValidationMessageHandler fail)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fail($"{kind}不能为空。");
        }
        else if (value.Contains('@'))
        {
            fail($"{kind}不能含 @，@ 是端口引用的分隔符。");
        }
        else if (value.Contains('/'))
        {
            fail($"{kind}不能含 /，/ 是节点路径的分隔符。");
        }
    }
}