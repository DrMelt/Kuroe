using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点不变量的统一入口：标识、结构、接线、输入绑定与 From 覆盖按固定顺序判定。
/// 静态来源解析与图级规则由调用方补充。</summary>
public static partial class NodeSpecRules
{
    /// <summary>按作用域检查一个节点的全部不变量，每条错误经 fail 报出。</summary>
    public static void Check(this NodeSpec node, NodeRuleContext context, ValidationMessageHandler fail)
    {
        node.CheckIdentity(context, fail);
        node.CheckStructure(node.Type?.Shape, context, fail);
        node.CheckWiring(context, fail);
        node.CheckBindings(fail);
        if (context == NodeRuleContext.FlowDefinition && (node.IsContainer || node.Spec is not null))
        {
            node.CheckFrom(fail);
            node.CheckBindingCoverage(node.EffectiveInputs, node.IsContainer, fail);
        }

        node.CheckInterface(node.Type?.Shape, fail);
    }
}