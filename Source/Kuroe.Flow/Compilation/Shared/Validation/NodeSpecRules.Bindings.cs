using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.StaticGraph.Nodes;
using Kuroe.Flow.Node.StaticGraph.Ports;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>输入绑定规则：绑定目标必须是已声明的静态输入端口。动态输入一律经 From，容器只有一组 From。
/// 覆盖要求按节点形态判定：执行节点静态输入要有绑定或默认值，容器静态输入要有绑定。</summary>
public static partial class NodeSpecRules
{
    /// <summary>检查绑定目标，逐条经 fail 报出。引用节点在库校验阶段尚未展开，目标由展开后判定。</summary>
    public static void CheckBindings(this NodeSpec node, ValidationMessageHandler fail)
    {
        if (node.Bindings is not { Count: > 0 } bindings)
        {
            return;
        }

        // 引用节点在库校验阶段尚未展开，目标输入与来源端口由流程展开后判定。
        if (node.Use is not null && node.Type is null)
        {
            return;
        }

        foreach (PortName name in bindings.Keys)
        {
            InputPort? target = node.FindInput(name);
            if (target is null)
            {
                fail($"绑定目标 {name.Value} 不是已声明的输入端口。");
                continue;
            }

            if (!target.Value.IsStatic)
            {
                fail($"绑定目标 {name.Value} 不是静态输入端口，动态输入一律经 From 绑定。");
            }
        }
    }

    /// <summary>检查输入端口是否都有值来源。容器的静态输入都要绑定，执行节点的静态输入要有绑定或默认值。</summary>
    public static void CheckBindingCoverage(
        this NodeSpec node,
        IReadOnlyList<InputPort> inputs,
        bool container,
        ValidationMessageHandler fail)
    {
        foreach (InputPort input in inputs)
        {
            bool bound = node.Bindings?.ContainsKey(input.Name) == true;
            if (container)
            {
                if (input.IsStatic && !bound)
                {
                    fail($"静态输入端口 {input.Name.Value} 没有绑定来源。");
                }

                continue;
            }

            if (input.IsStatic && !bound && string.IsNullOrWhiteSpace(input.Default))
            {
                fail($"静态输入端口 {input.Name.Value} 没有绑定来源或默认值。");
            }
        }
    }
}