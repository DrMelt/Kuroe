using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>节点标识规则：名称、标签、保留名与路径分隔符。</summary>
public sealed partial record NodeSpec
{
    /// <summary>按作用域检查节点标识：定义条目按节点名，引用条目按标签。</summary>
    public void CheckIdentity(NodeRuleContext context, Action<string> fail)
    {
        if (context == NodeRuleContext.Reference || Use is not null || Name is null)
        {
            CheckReference(fail);
            return;
        }

        CheckDefinition(fail);
    }

    /// <summary>检查定义条目的节点名，标签必须取节点名文本。</summary>
    private void CheckDefinition(Action<string> fail)
    {
        if (Name is not { } name)
        {
            fail("节点名不能为空。");

            return;
        }

        Check(name.Value, "节点名", StartNode.ReservedName.Value, "名", fail);

        if (Label.Value != name.Value)
        {
            fail($"定义条目的标签取节点名 {name.Value} 的文本，不能另写标识。");
        }
    }

    /// <summary>检查引用条目的标签。</summary>
    private void CheckReference(Action<string> fail) =>
        Check(Label.Value, "节点标签", StartNode.ReservedLabel.Value, "标签", fail);

    private static void Check(string value, string kind, string reserved, string reservedKind, Action<string> fail)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fail($"{kind}不能为空。");
        }
        else if (value == reserved)
        {
            fail($"{kind} {value} 是流程起点的保留{reservedKind}。");
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