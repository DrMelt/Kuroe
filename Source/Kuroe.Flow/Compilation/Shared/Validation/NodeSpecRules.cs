using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点不变量的统一入口：标识、结构、接线、静态端口、接口与校验参数按固定顺序判定。
/// 阶段专属规则不在这里，From 覆盖、静态来源与图级规则由调用方补充。</summary>
public static partial class NodeSpecRules
{
    /// <summary>按作用域检查一个节点的全部不变量，每条错误经 fail 报出。</summary>
    public static void Check(this NodeSpec node, NodeRuleContext context, Action<string> fail)
    {
        node.CheckIdentity(context, fail);
        node.CheckStructure(node.Type?.Shape, context, fail);
        node.CheckWiring(context, fail);
        node.CheckStatic(fail);
        if (node.Spec is not null && context != NodeRuleContext.LibraryDefinition)
        {
            node.CheckStaticCoverage(fail);
        }

        node.CheckInterface(node.Type?.Shape, fail);
        node.Spec?.Validate?.Check(fail);
    }
}