using Flow = Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Flows.StaticGraph;

/// <summary>节点接口的静态规则：输入与输出端口名在同一节点内唯一，端口类型落在类型形状给出的值域，
/// WorkItems 与 Context 输出端口至多声明一个，系统指令块非空，Question 只属于等待外部输入的节点。
/// 流程校验与节点库校验共用这一份，报错出口由调用方给出。</summary>
internal static class NodeInterfaceRules
{
    /// <summary>逐条检查一个节点的端口与指令声明，每条错误经 fail 报出。shape 给出端口类型值域与 Question 归属，
    /// 类型未注册时跳过值域检查，由装配期的类型校验负责。</summary>
    public static void Check(Flow.NodeSpec node, Flow.NodeTypeShape? shape, Action<string> fail)
    {
        foreach (Flow.PortName port in Duplicates([.. node.Inputs.Select(port => port.Name)]))
        {
            fail($"输入端口 {port.Value} 重复。");
        }

        foreach (Flow.PortName port in Duplicates([.. node.Outputs.Select(port => port.Name)]))
        {
            fail($"输出端口 {port.Value} 重复。");
        }

        if (shape is { } declared)
        {
            foreach (Flow.InputPort port in node.Inputs.Where(port => !declared.InputTypes.Contains(port.Type)))
            {
                fail($"输入端口 {port.Name.Value} 的类型只能是 {Types(declared.InputTypes)}。");
            }

            foreach (string port in node.Outputs.Where(port => !declared.OutputTypes.Contains(port.Type)).Select(port => port.Name.Value))
            {
                fail($"输出端口 {port} 的类型只能是 {Types(declared.OutputTypes)}。");
            }
        }

        if (node.Execution is not { } executable || shape is { DispatchesRuns: false })
        {
            return;
        }

        if (executable.Question is { Length: > 0 })
        {
            fail("Question 只属于等待外部输入的节点。");
        }

        if (node.Outputs.Count(port => port.Type == Flow.PortType.WorkItems) > 1)
        {
            fail("WorkItems 输出端口至多声明一个。");
        }

        if (node.Outputs.Count(port => port.Type == Flow.PortType.Context) > 1)
        {
            fail("Context 输出端口至多声明一个。");
        }

        foreach (string _ in node.SystemPrompt.Where(string.IsNullOrWhiteSpace))
        {
            fail("系统指令块不能为空。");
        }
    }

    /// <summary>端口类型值域的展示文本。</summary>
    private static string Types(IReadOnlyList<Flow.PortType> types) => types.Count switch
    {
        0 => "（无）",
        1 => types[0].ToString(),
        _ => $"{string.Join("、", types.Take(types.Count - 1).Select(type => type.ToString()))} 或 {types[^1]}",
    };

    private static IEnumerable<Flow.PortName> Duplicates(IReadOnlyList<Flow.PortName> names) =>
        names.GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key);
}
