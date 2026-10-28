using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>静态端口规则：绑定目标必须是已声明的静态输入，无绑定与默认值的静态输入不能成立。来源在装载期校验。</summary>
public sealed partial record NodeSpec
{
    /// <summary>检查静态绑定的目标，逐条经 fail 报出。容器的字段限制由接线规则判定。</summary>
    public void CheckStatic(Action<string> fail)
    {
        if (Static is not { Count: > 0 } bindings)
        {
            return;
        }

        // 引用节点在库校验阶段尚未展开，目标输入与来源端口由流程展开后判定。
        if (Use is not null && Type is null)
        {
            return;
        }

        foreach (PortName name in bindings.Keys.Where(name => FindInput(name) is not { IsStatic: true }))
        {
            fail($"静态绑定目标 {name.Value} 不是已声明的静态输入端口。");
        }
    }

    /// <summary>检查静态输入是否都由绑定或默认值提供。库定义的静态输入由引用处提供，不在此判定。</summary>
    public void CheckStaticCoverage(Action<string> fail)
    {
        foreach (InputPort input in EffectiveInputs.Where(port => port.IsStatic))
        {
            if ((Static is null || !Static.ContainsKey(input.Name)) && string.IsNullOrWhiteSpace(input.Default))
            {
                fail($"静态输入端口 {input.Name.Value} 没有绑定来源或默认值。");
            }
        }
    }
}