using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Runtime.Nodes;

namespace Kuroe.Flow.Runtime.Graph;

/// <summary>为执行节点装配执行输入：按生效组的入边取各输入端口的内容与出处，未连接的 Text 默认端口追加声明值。
/// 信号输入不取内容，Context 输入端口的取值置于最前，其余按组内连接序。</summary>
internal static class ExecutionInputBuilder
{
    /// <summary>单条取值文本的上限。</summary>
    private const int TextLimit = 2000;

    /// <summary>为执行节点装配这一轮的输入。</summary>
    internal static ExecutionInput Build(
        TaskId id,
        RuntimeExecutable node,
        INodeContext context,
        INodeDeclaration? declaration)
    {
        List<ContextValue> contextInput = [];
        List<ContextValue> values = [];
        HashSet<PortName> connected = [];
        int group = node.LastInputGroup ?? 0;
        foreach (FlowEdge edge in context.Graph.Incoming(node.Path).Where(edge => edge.Group == group))
        {
            connected.Add(edge.TargetInput);
            if (edge.Type.Semantics == PortSemantics.Signal)
            {
                // 信号语义不取内容，来源产出不进输入，只作启动信号；Trigger 版只额外携带触发值
                continue;
            }

            List<ContextValue> target = edge.Type.Semantics == PortSemantics.Context ? contextInput : values;
            foreach (ContextValue value in context.Runtime(edge.Source).OutputValues(context, edge.SourcePort))
            {
                target.Add(Truncate(value));
            }
        }

        foreach (InputPort port in node.Executable.Inputs)
        {
            if (connected.Contains(port.Name) || port.Type != PortTypes.Text || port.Default is not { Length: > 0 } text)
            {
                continue;
            }

            values.Add(Truncate(new ContextValue(new DefaultSource(node.Path, port.Name), text)));
        }

        // 上下文输入端口的内容与系统指令一起构成请求前缀段，置于取值最前
        values.InsertRange(0, contextInput);

        return new ExecutionInput
        {
            Task = id,
            Node = node.Path,
            Type = node.Executable.Type,
            Declaration = declaration,
            ExecutionCount = node.Executions + 1,
            Inputs = values,
            OutputPorts = node.Executable.Outputs,
        };
    }

    /// <summary>取值文本过长时截断，出处原样保留。</summary>
    private static ContextValue Truncate(ContextValue value) =>
        value.Text.Length <= TextLimit
            ? value
            : value with { Text = string.Concat(value.Text.AsSpan(0, TextLimit), "…") };
}