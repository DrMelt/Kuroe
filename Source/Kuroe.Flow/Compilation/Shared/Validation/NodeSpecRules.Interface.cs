using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点接口的静态规则：输入与输出端口名在同一节点内唯一，端口类型落在类型形状给出的值域，
/// 输出按 Value、Ref 与 Constant 判定来源。流程校验与节点库校验共用这一份，报错出口由调用方给出。</summary>
public static partial class NodeSpecRules
{
    /// <summary>逐条检查一个节点的端口声明，每条错误经 fail 报出。shape 给出端口类型值域与推进方式，
    /// 类型未注册时跳过值域检查，由装配期的类型校验负责。</summary>
    public static void CheckInterface(
        this NodeSpec node,
        NodeTypeShape? shape,
        Action<string> fail)
    {
        bool container = shape?.Organizes ?? node.IsContainer;
        bool organizes = node.IsContainer;
        IReadOnlyList<InputPort> inputs = node.EffectiveInputs;
        foreach (PortName port in Duplicates([.. inputs.Select(port => port.Name)]))
        {
            fail($"输入端口 {port.Value} 重复。");
        }

        foreach (InputPort port in inputs.Where(port => port.IsStatic && port.Type != PortTypes.Text))
        {
            fail($"静态输入端口 {port.Name.Value} 的类型只能是 Text。");
        }

        foreach (InputPort port in inputs.Where(port => port.IsStatic && port.Trigger))
        {
            fail($"静态输入端口 {port.Name.Value} 不能使用触发。");
        }

        foreach (InputPort port in inputs.Where(port => port.Role == InputRole.Control && port.IsStatic))
        {
            fail($"控制输入端口 {port.Name.Value} 不能是静态输入。");
        }

        foreach (InputPort port in inputs.Where(port => port.Trigger && port.Type.Semantics == PortSemantics.Context))
        {
            fail($"Context 输入端口 {port.Name.Value} 不能使用触发。");
        }

        if (shape is { } declared)
        {
            foreach (InputPort port in inputs.Where(port => !declared.InputTypes.Contains(port.Type)))
            {
                fail($"输入端口 {port.Name.Value} 的类型只能是 {Types(declared.InputTypes)}。");
            }
        }

        if (container)
        {
            foreach (InputPort port in inputs.Where(port => port.IsStatic && port.Default is not null))
            {
                fail($"静态输入端口 {port.Name.Value} 在容器上不能声明默认值，值由 In 绑定提供。");
            }
        }

        foreach (PortName port in Duplicates([.. node.Outputs.Select(port => port.Name)]))
        {
            fail($"输出端口 {port.Value} 重复。");
        }

        if (shape is { } declaredOutput)
        {
            foreach (string port in node.Outputs.Where(port => !declaredOutput.OutputTypes.Contains(port.Type)).Select(port => port.Name.Value))
            {
                fail($"输出端口 {port} 的类型只能是 {Types(declaredOutput.OutputTypes)}。");
            }
        }

        foreach (Port port in node.Outputs)
        {
            CheckOutputSource(node, port, organizes, fail);

            if (port.IsConstant && port.Trigger)
            {
                fail($"常量输出端口 {port.Name.Value} 不能使用触发。");
            }

            if (port.Trigger && port.Type.Semantics == PortSemantics.Context)
            {
                fail($"Context 输出端口 {port.Name.Value} 不能使用触发。");
            }
        }

        if (shape is { Organizes: false } executable)
        {
            foreach (InputPort port in inputs.Where(port => port.Default is not null))
            {
                if (port.IsStatic)
                {
                    if (string.IsNullOrWhiteSpace(port.Default))
                    {
                        fail($"静态输入端口 {port.Name.Value} 的默认值不能为空。");
                    }

                    continue;
                }

                if (!executable.DispatchesExecutions || port.Type != PortTypes.Text)
                {
                    fail($"输入端口 {port.Name.Value} 的默认值只用于派发执行的节点的 Text 输入端口。");
                }
                else if (string.IsNullOrWhiteSpace(port.Default))
                {
                    fail($"输入端口 {port.Name.Value} 的默认值不能为空。");
                }
            }
        }

        if (node.Spec is null || shape is { DispatchesExecutions: false })
        {
            return;
        }

        if (node.Outputs.Count(port => port.Type.Semantics == PortSemantics.Context && !port.IsConstant) > 1)
        {
            fail("Context 输出端口至多声明一个。");
        }
    }

    /// <summary>输出端口来源：常量输出只接受固定值或常量来源，动态输出不接受固定值，转发来源只用于组织子节点的节点。</summary>
    private static void CheckOutputSource(NodeSpec node, Port port, bool organizes, Action<string> fail)
    {
        string name = port.Name.Value;
        if (port.Value is not null && port.Ref is not null)
        {
            fail($"输出端口 {name} 不能同时写 Value 与 Ref。");
            return;
        }

        if (port.IsConstant)
        {
            if (port.Type != PortTypes.Text)
            {
                fail($"常量输出端口 {name} 的类型只能是 Text。");
            }

            if (port.Value is { } value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    fail($"常量输出端口 {name} 的 Value 不能为空。");
                }

                return;
            }

            if (port.Ref is not { } reference)
            {
                fail($"常量输出端口 {name} 必须写 Value 或 Ref。");
                return;
            }

            if (reference.Source is null)
            {
                if (organizes)
                {
                    fail($"容器常量输出端口 {name} 不能引用自身输入 {reference.Display}，只能引用子树成员的常量输出。");
                    return;
                }

                if (node.FindInput(reference.Port) is not { IsStatic: true })
                {
                    fail($"常量输出端口 {name} 的来源 {reference.Display} 不是本节点的静态输入。");
                }

                return;
            }

            if (!organizes)
            {
                fail($"常量输出端口 {name} 的 Ref 只能引用本节点的静态输入，写 @端口。");
            }

            return;
        }

        if (port.Value is not null)
        {
            fail($"动态输出端口 {name} 不能写 Value。");
        }

        if (organizes)
        {
            if (port.Ref is null)
            {
                fail($"输出端口 {name} 必须写 Value 或 Ref。");
            }

            return;
        }

        if (port.Ref is not null)
        {
            fail($"动态输出端口 {name} 不能写 Ref，执行产出由节点交回。");
        }
    }

    /// <summary>端口类型值域的展示文本。</summary>
    private static string Types(IReadOnlyList<PortType> types) => types.Count switch
    {
        0 => "（无）",
        1 => types[0].ToString(),
        _ => $"{string.Join("、", types.Take(types.Count - 1).Select(type => type.ToString()))} 或 {types[^1]}",
    };

    private static IEnumerable<PortName> Duplicates(IReadOnlyList<PortName> names) =>
        names.GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key);
}
