using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Runtime.Nodes;

namespace Kuroe.Flow.Runtime.Graph;

/// <summary>为执行节点装配执行输入：按节点层级装入的输入取各输入端口的取值与出处，未连接的 Text 默认端口追加声明值。
/// Context 输入端口的取值置于最前，其余按组内连接序。</summary>
internal static class ExecutionInputBuilder
{
    /// <summary>单条取值文本的上限。</summary>
    private const int TextLimit = 2000;

    /// <summary>为执行节点装配这一轮的输入。</summary>
    internal static ExecutionInput Build(
        TaskId id,
        RuntimeExecutable node,
        NodeInputValues inputs,
        INodeDeclaration? declaration)
    {
        List<InputValue> contextInput = [];
        List<InputValue> values = [];
        Dictionary<PortName, List<ContextValue>> textValues = [];
        foreach (NodeInputValue input in inputs.Ordered)
        {
            InputValue value = new(input.TargetInput, Truncate(input.Value));
            List<InputValue> target = input.Type.Semantics == PortSemantics.Context ? contextInput : values;
            target.Add(value);
            AddText(textValues, value);
        }

        foreach (InputPort port in node.Executable.Inputs)
        {
            if (port.IsStatic
                || inputs.Connected(port.Name)
                || port.Type != PortTypes.Text
                || port.Default is not { Length: > 0 } text)
            {
                continue;
            }

            InputValue value = new(
                port.Name,
                Truncate(new ContextValue(new DefaultSource(node.Path, port.Name), text)));
            values.Add(value);
            AddText(textValues, value);
        }

        // 上下文输入端口的内容与系统指令一起构成请求前缀段，置于取值最前
        values.InsertRange(0, contextInput);

        // 端口文本与取值来自同一份截断结果，静态端口优先
        Dictionary<PortName, string> texts = new(inputs.StaticTexts.Texts);
        foreach ((PortName port, List<ContextValue> list) in textValues)
        {
            if (!texts.ContainsKey(port) && ContextValues.Join(list) is { } text)
            {
                texts[port] = text;
            }
        }

        return new ExecutionInput
        {
            Task = id,
            Node = node.Path,
            Type = node.Executable.Type,
            Declaration = declaration,
            ExecutionCount = node.Executions + 1,
            Inputs = values,
            Texts = texts,
            OutputPorts = node.Executable.Outputs,
        };
    }

    /// <summary>记录一条取值的文本，按端口保持到达顺序。</summary>
    private static void AddText(Dictionary<PortName, List<ContextValue>> values, InputValue value)
    {
        if (!values.TryGetValue(value.Port, out List<ContextValue>? list))
        {
            list = [];
            values[value.Port] = list;
        }

        list.Add(value.Value);
    }

    /// <summary>取值文本过长时截断，出处原样保留。</summary>
    private static ContextValue Truncate(ContextValue value) =>
        value.Text.Length <= TextLimit
            ? value
            : value with { Text = string.Concat(value.Text.AsSpan(0, TextLimit), "…") };
}