using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>From 接线规则：动态输入经 From 连接，静态输入经 Bindings 绑定。容器只有一组 From，
/// 成员通过 @端口 读取的传入来源因此唯一。触发条件只用于触发端口。</summary>
public static partial class NodeSpecRules
{
    /// <summary>检查 From 的目标、覆盖与条件，按节点形态判定需要的动态输入。</summary>
    public static void CheckFrom(this NodeSpec node, ValidationMessageHandler fail)
    {
        IReadOnlyList<InputPort> declared = node.DeclaredInputs;
        bool container = node.IsContainer;
        var required = new HashSet<PortName>([.. declared
            .Where(port => !port.IsStatic)
            .Where(port => container || port.Default is null)
            .Select(port => port.Name)]);

        if (container && node.From.Count > 1)
        {
            fail("容器只能写一组 From，成员经 @端口 读取的传入来源必须唯一。");
        }

        if (required.Count > 0 && node.From.Count == 0)
        {
            fail(container
                ? "容器声明动态输入端口时必须给出一组 From。"
                : "声明动态输入端口的节点必须至少给出一组 From 启动条件。");
        }

        foreach (FromGroup group in node.From)
        {
            var covered = new HashSet<PortName>();
            foreach (FromLink link in group.Links)
            {
                PortName input = link.Input;
                InputPort? declaredInput = node.FindInput(input);
                if (declaredInput is { IsStatic: true })
                {
                    fail($"From 不能连接静态输入端口 {input}，静态输入用 Bindings 绑定。");
                }
                else if (declaredInput is null)
                {
                    fail($"From 连接的目标输入端口 {input} 没有在节点上声明。");
                }
                else if (link.Condition != TriggerCondition.Positive && declaredInput is { Trigger: false })
                {
                    fail($"From 连接的目标输入端口 {input} 的触发条件只用于触发端口。");
                }

                if (!covered.Add(input))
                {
                    fail($"From 组内的目标输入端口 {input} 出现多次。");
                }
            }

            foreach (PortName missing in required.Where(port => !covered.Contains(port)))
            {
                fail($"From 组没有覆盖输入端口 {missing.Value}。");
            }
        }
    }
}