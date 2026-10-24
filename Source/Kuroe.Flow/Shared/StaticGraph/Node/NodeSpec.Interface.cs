using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>节点接口的静态规则：输入与输出端口名在同一节点内唯一，端口类型落在类型形状给出的值域，
/// 声明节点的 Context 输出端口至多一个。流程校验与节点库校验共用这一份，报错出口由调用方给出。</summary>
public sealed partial record NodeSpec
{
    /// <summary>逐条检查一个节点的端口声明，每条错误经 fail 报出。shape 给出端口类型值域与推进方式，
    /// 类型未注册时跳过值域检查，由装配期的类型校验负责。</summary>
    public void CheckInterface(NodeTypeShape? shape, Action<string> fail)
    {
        foreach (PortName port in Duplicates([.. Inputs.Select(port => port.Name)]))
        {
            fail($"输入端口 {port.Value} 重复。");
        }

        foreach (PortName port in Duplicates([.. Outputs.Select(port => port.Name)]))
        {
            fail($"输出端口 {port.Value} 重复。");
        }

        if (shape is { } declared)
        {
            foreach (InputPort port in Inputs.Where(port => !declared.InputTypes.Contains(port.Type)))
            {
                fail($"输入端口 {port.Name.Value} 的类型只能是 {Types(declared.InputTypes)}。");
            }

            foreach (string port in Outputs.Where(port => !declared.OutputTypes.Contains(port.Type)).Select(port => port.Name.Value))
            {
                fail($"输出端口 {port} 的类型只能是 {Types(declared.OutputTypes)}。");
            }

            foreach (InputPort port in Inputs.Where(port => port.Default is not null))
            {
                if (!declared.DispatchesExecutions || port.Type != PortTypes.Text)
                {
                    fail($"输入端口 {port.Name.Value} 的默认值只用于派发执行的节点的 Text 输入端口。");
                }
                else if (string.IsNullOrWhiteSpace(port.Default))
                {
                    fail($"输入端口 {port.Name.Value} 的默认值不能为空。");
                }
            }
        }

        if (Spec is null || shape is { DispatchesExecutions: false })
        {
            return;
        }

        if (Outputs.Count(port => port.Type.Semantics == PortSemantics.Context) > 1)
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
