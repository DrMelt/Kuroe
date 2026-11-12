using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>静态输入与常量输出的结构解析：沿绑定链找到值终点，只依赖图，不接触注入文本。
/// 所有节点共用同一套输出来源，开始端口给固定值或注入绑定，常量输出沿输出来源或静态输入下钻，
/// 没有固定值与来源引用的常量输出是 Once 节点唯一一次执行的运行期常量。</summary>
public static class StaticSources
{
    /// <summary>解析一个静态输入端口的来源终点，无法解析时给出错误说明。</summary>
    public static StaticResolution ResolveInput(NodeGraph graph, NodePath node, PortName port) =>
        ResolvePort(graph, BindingSource.ForNode(node), port, StaticPortSide.Input, []);

    /// <summary>解析一个常量输出端口的来源终点，无法解析时给出错误说明。</summary>
    public static StaticResolution ResolveOutput(NodeGraph graph, NodePath node, PortName port) =>
        ResolvePort(graph, BindingSource.ForNode(node), port, StaticPortSide.Output, []);

    private static StaticResolution ResolvePort(
        NodeGraph graph,
        BindingSource source,
        PortName port,
        StaticPortSide side,
        HashSet<StaticPortKey> visiting)
    {
        var key = new StaticPortKey(source, port, side);
        if (!visiting.Add(key))
        {
            return StaticResolution.Missing(source, port, $"端口 {port} 的来源形成环。");
        }

        try
        {
            if (source.IsStart)
            {
                return ResolveStart(graph, port);
            }

            if (source.Node is not { } node)
            {
                return StaticResolution.Missing(source, port, $"静态来源 {source.Display} 不在流程里。");
            }

            return Find(graph, node) switch
            {
                GraphNode inputNode when side == StaticPortSide.Input => ResolveInput(graph, inputNode, port, visiting),
                ContainerNode container => ResolveOutput(graph, container, port, visiting),
                ExecutableNode executable => ResolveOutput(graph, executable, port, visiting),
                _ => StaticResolution.Missing(source, port, $"静态来源 {source.Display} 不在流程里。"),
            };
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    /// <summary>开始端口的来源：常量端口给固定值或注入绑定。</summary>
    private static StaticResolution ResolveStart(NodeGraph graph, PortName port)
    {
        BindingSource source = BindingSource.ForStart(port);
        if (graph.StartPorts.Find(port) is not { } declared)
        {
            return StaticResolution.Missing(source, port, $"开始没有输出端口 {port}。");
        }

        if (!declared.IsConstant)
        {
            return StaticResolution.Missing(source, port, $"开始端口 {port} 不是常量端口。");
        }

        return declared.Value is { Length: > 0 } text
            ? StaticResolution.StartFixed(port, text)
            : StaticResolution.StartInjected(port);
    }

    /// <summary>节点的常量输出：固定值、内部来源端口或 Once 节点唯一一次执行的产出。</summary>
    private static StaticResolution ResolveOutput(
        NodeGraph graph,
        GraphNode node,
        PortName port,
        HashSet<StaticPortKey> visiting)
    {
        if (node.FindPort(port) is not { } output || !output.IsConstant)
        {
            return StaticResolution.Missing(node.Path, port, $"节点 {node.Path} 的输出端口 {port} 不是常量端口。");
        }

        if (output.Value is { Length: > 0 } text)
        {
            return StaticResolution.Literal(node.Path, port, text);
        }

        if (node.OutputBindings?.TryGetValue(port, out OutputBinding source) != true)
        {
            return node is ExecutableNode { Spec.Emission: NodeEmission.Once }
                ? StaticResolution.RuntimeConstant(node.Path, port)
                : StaticResolution.Missing(node.Path, port,
                    $"常量输出端口 {port} 没有来源，只有 Emission 为 Once 的节点可以由执行产出。");
        }

        StaticPortSide side = source.Source == node.Path ? StaticPortSide.Input : StaticPortSide.Output;

        return ResolvePort(graph, BindingSource.ForNode(source.Source, source.Port), source.Port, side, visiting);
    }

    /// <summary>节点的静态输入：绑定来源优先，其次默认值。执行节点与容器共用这一条链。</summary>
    private static StaticResolution ResolveInput(
        NodeGraph graph,
        GraphNode node,
        PortName port,
        HashSet<StaticPortKey> visiting)
    {
        if (node.FindInput(port) is not { IsStatic: true } input)
        {
            return StaticResolution.Missing(node.Path, port, $"静态输入端口 {port} 不是静态输入。");
        }

        if (node.InputBindings?.TryGetValue(port, out InputBinding? source) == true
            && source is { SourcePort: { } sourcePort })
        {
            return ResolvePort(graph, source.Source, sourcePort, StaticPortSide.Output, visiting);
        }

        return input.Default is { Length: > 0 } defaultValue
            ? StaticResolution.Default(node.Path, port, defaultValue)
            : StaticResolution.Missing(node.Path, port, $"静态输入端口 {port} 没有绑定来源或默认值。");
    }

    /// <summary>按路径取节点，未收录时为空。</summary>
    private static GraphNode? Find(NodeGraph graph, NodePath path) =>
        graph.Nodes.FirstOrDefault(node => node.Path == path);

    private enum StaticPortSide
    {
        Input,
        Output,
    }

    private readonly record struct StaticPortKey(BindingSource Source, PortName Port, StaticPortSide Side);
}

/// <summary>静态输入与常量输出解析的终点。Missing 带错误说明，其余带固定值、默认值、开始端口或运行期常量来源。</summary>
public readonly record struct StaticResolution(
    StaticSourceKind Kind,
    BindingSource Source,
    PortName Port,
    string? Text,
    string? Error)
{
    /// <summary>开始端口的固定值。</summary>
    public static StaticResolution StartFixed(PortName port, string text) =>
        new(StaticSourceKind.StartFixed, BindingSource.ForStart(port), port, text, null);

    /// <summary>开始端口的非固定值：宿主给值或提交输入。</summary>
    public static StaticResolution StartInjected(PortName port) =>
        new(StaticSourceKind.StartInjected, BindingSource.ForStart(port), port, null, null);

    /// <summary>常量输出的字面量。</summary>
    public static StaticResolution Literal(NodePath node, PortName port, string text) =>
        new(StaticSourceKind.Literal, BindingSource.ForNode(node), port, text, null);

    /// <summary>Once 节点唯一一次执行产出的常量。</summary>
    public static StaticResolution RuntimeConstant(NodePath node, PortName port) =>
        new(StaticSourceKind.RuntimeConstant, BindingSource.ForNode(node), port, null, null);

    /// <summary>静态输入的默认值。</summary>
    public static StaticResolution Default(NodePath node, PortName port, string text) =>
        new(StaticSourceKind.Default, BindingSource.ForNode(node), port, text, null);

    /// <summary>无法解析，给出错误说明。</summary>
    public static StaticResolution Missing(NodePath node, PortName port, string error) =>
        Missing(BindingSource.ForNode(node), port, error);

    /// <summary>无法解析，给出错误说明。</summary>
    public static StaticResolution Missing(BindingSource source, PortName port, string error) =>
        new(StaticSourceKind.Missing, source, port, null, error);
}

/// <summary>静态输入与常量输出解析终点的种类。</summary>
public enum StaticSourceKind
{
    /// <summary>开始端口的固定值。</summary>
    StartFixed,

    /// <summary>开始端口的非固定值，来自宿主给值或提交输入。</summary>
    StartInjected,

    /// <summary>常量输出的字面量。</summary>
    Literal,

    /// <summary>Once 节点唯一一次执行产出的常量。</summary>
    RuntimeConstant,

    /// <summary>静态输入的默认值。</summary>
    Default,

    /// <summary>无法解析。</summary>
    Missing,
}
