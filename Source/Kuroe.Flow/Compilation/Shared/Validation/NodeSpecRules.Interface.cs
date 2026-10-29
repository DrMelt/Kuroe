using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点接口的静态规则：输入与输出端口名在同一节点内唯一，端口类型落在类型形状给出的值域，
/// 声明节点的 Context 输出端口至多一个。流程校验与节点库校验共用这一份，报错出口由调用方给出。</summary>
public static partial class NodeSpecRules
{
    /// <summary>逐条检查一个节点的端口声明，每条错误经 fail 报出。shape 给出端口类型值域与推进方式，
    /// 类型未注册时跳过值域检查，由装配期的类型校验负责。</summary>
    public static void CheckInterface(this NodeSpec node, NodeTypeShape? shape, Action<string> fail)
    {
        IReadOnlyList<InputPort> inputs = node.EffectiveInputs;
        foreach (PortName port in Duplicates([.. inputs.Select(port => port.Name)]))
        {
            fail($"输入端口 {port.Value} 重复。");
        }

        foreach (InputPort fixedPort in (node.Type?.Shape.StaticInputs ?? [])
            .Where(fixedPort => node.Inputs.Find(fixedPort.Name) is { IsStatic: false }))
        {
            fail($"输入端口 {fixedPort.Name.Value} 是节点类型的静态端口，不能声明为动态端口。");
        }

        foreach (InputPort port in inputs.Where(port => port.IsStatic && port.Type != PortTypes.Text))
        {
            fail($"静态输入端口 {port.Name.Value} 的类型只能是 Text。");
        }

        if (shape is { } declared)
        {
            foreach (InputPort port in inputs.Where(port => !declared.InputTypes.Contains(port.Type)))
            {
                fail($"输入端口 {port.Name.Value} 的类型只能是 {Types(declared.InputTypes)}。");
            }
        }

        if (shape is { Organizes: true })
        {
            // 容器的输出是子树绑定，容器的静态输入由 In 绑定提供值
            foreach (InputPort port in inputs.Where(port => port.IsStatic && port.Default is not null))
            {
                fail($"静态输入端口 {port.Name.Value} 在容器上不能声明默认值，值由 In 绑定提供。");
            }

            return;
        }

        foreach (PortName port in Duplicates([.. node.Outputs.Select(port => port.Name)]))
        {
            fail($"输出端口 {port.Value} 重复。");
        }

        if (shape is { } executable)
        {
            foreach (string port in node.Outputs.Where(port => !executable.OutputTypes.Contains(port.Type)).Select(port => port.Name.Value))
            {
                fail($"输出端口 {port} 的类型只能是 {Types(executable.OutputTypes)}。");
            }

            foreach (Port port in node.Outputs.Where(port => port.IsStatic))
            {
                fail($"输出端口 {port.Name.Value} 不能是静态端口，执行节点的产出都是动态的。");
            }

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

        if (node.Outputs.Count(port => port.Type.Semantics == PortSemantics.Context) > 1)
        {
            fail("Context 输出端口至多声明一个。");
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
