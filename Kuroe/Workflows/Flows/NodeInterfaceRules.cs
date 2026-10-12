using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>节点接口的静态规则：输入与输出端口名在同一节点内唯一，端口类型落在各自值域，
/// Items 与 Context 输出端口至多声明一个，系统指令块非空，Question 只属于输入节点。
/// 流程校验与节点库校验共用这一份，报错出口由调用方给出。</summary>
internal static class NodeInterfaceRules
{
    /// <summary>逐条检查一个节点的端口与指令声明，每条错误经 fail 报出。</summary>
    public static void Check(Flow.NodeSpec node, Action<string> fail)
    {
        foreach (Flow.PortName port in Duplicates([.. node.Inputs.Select(port => port.Name)]))
        {
            fail($"输入端口 {port.Value} 重复。");
        }

        foreach (Flow.InputPort port in node.Inputs.Where(port =>
            port.Type is not (Flow.PortType.Text or Flow.PortType.Context or Flow.PortType.Signal)))
        {
            fail($"输入端口 {port.Name.Value} 的类型只能是 Text、Context 或 Signal。");
        }

        foreach (Flow.Port port in node.Outputs.Where(port =>
            port.Type is not (Flow.PortType.Text or Flow.PortType.WorkItems or Flow.PortType.Context)))
        {
            fail($"输出端口 {port.Name.Value} 的类型只能是 Text、WorkItems 或 Context。");
        }

        foreach (Flow.PortName port in Duplicates([.. node.Outputs.Select(port => port.Name)]))
        {
            fail($"输出端口 {port.Value} 重复。");
        }

        if (node.Execution is not { } executable || node.Kind == Flow.NodeKind.Input)
        {
            return;
        }

        if (executable.Question is { Length: > 0 })
        {
            fail("Question 只属于输入节点。");
        }

        if (node.Outputs.Count(port => port.Type == Flow.PortType.WorkItems) > 1)
        {
            fail("拆分支票端口至多声明一个。");
        }

        if (node.Outputs.Count(port => port.Type == Flow.PortType.Context) > 1)
        {
            fail("装配上下文端口至多声明一个。");
        }

        foreach (string _ in node.SystemPrompt.Where(string.IsNullOrWhiteSpace))
        {
            fail("系统指令块不能为空。");
        }
    }

    private static IEnumerable<Flow.PortName> Duplicates(IReadOnlyList<Flow.PortName> names) =>
        names.GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key);
}
