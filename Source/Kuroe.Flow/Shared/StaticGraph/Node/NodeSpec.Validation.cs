namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>节点不变量的统一入口：标识、结构、接线、接口与校验参数按固定顺序判定。
/// 阶段专属规则不在这里，模型选择、From 覆盖与图级规则由调用方补充。</summary>
public sealed partial record NodeSpec
{
    /// <summary>按作用域检查一个节点的全部不变量，每条错误经 fail 报出。</summary>
    public void Check(NodeRuleContext context, Action<string> fail)
    {
        CheckIdentity(context, fail);
        CheckStructure(Type?.Shape, context, fail);
        CheckWiring(context, fail);
        if (!IsContainer)
        {
            CheckInterface(Type?.Shape, fail);
        }

        Spec?.Validate?.Check(fail);
    }
}