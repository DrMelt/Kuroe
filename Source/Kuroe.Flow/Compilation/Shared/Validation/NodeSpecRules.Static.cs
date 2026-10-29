using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>静态端口规则：绑定目标必须是已声明的静态输入，无绑定与默认值的静态输入不能成立。来源在装载期校验。</summary>
public static partial class NodeSpecRules
{
    /// <summary>检查静态绑定的目标，逐条经 fail 报出。容器的字段限制由接线规则判定。</summary>
    public static void CheckStatic(this NodeSpec node, Action<string> fail)
    {
        if (node.Static is not { Count: > 0 } bindings)
        {
            return;
        }

        // 引用节点在库校验阶段尚未展开，目标输入与来源端口由流程展开后判定。
        if (node.Use is not null && node.Type is null)
        {
            return;
        }

        foreach (PortName name in bindings.Keys.Where(name => node.FindInput(name) is not { IsStatic: true }))
        {
            fail($"静态绑定目标 {name.Value} 不是已声明的静态输入端口。");
        }
    }

    /// <summary>检查静态输入是否都由绑定或默认值提供。库定义的静态输入由引用处提供，不在此判定。</summary>
    public static void CheckStaticCoverage(this NodeSpec node, Action<string> fail)
    {
        foreach (InputPort input in node.EffectiveInputs.Where(port => port.IsStatic))
        {
            if ((node.Static is null || !node.Static.ContainsKey(input.Name)) && string.IsNullOrWhiteSpace(input.Default))
            {
                fail($"静态输入端口 {input.Name.Value} 没有绑定来源或默认值。");
            }
        }
    }
}