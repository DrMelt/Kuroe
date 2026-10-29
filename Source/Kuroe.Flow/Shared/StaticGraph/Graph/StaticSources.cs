using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>静态绑定的结构解析：沿绑定链找到值终点，只依赖图，不接触注入文本。
/// 开始端口给固定值或注入绑定，容器静态输出沿绑定下钻，执行节点取静态输入的绑定或默认值。
/// 执行节点没有静态输出，不能作为静态来源。</summary>
public static class StaticSources
{
    /// <summary>解析一个静态端口的来源终点，无法解析时给出错误说明。</summary>
    public static StaticResolution Resolve(INodeGraph graph, NodePath node, PortName port) =>
        ResolveTarget(graph, node, port, []);

    /// <summary>解析目标端口：开始、执行节点的静态输入或容器的静态输出。</summary>
    private static StaticResolution ResolveTarget(
        INodeGraph graph,
        NodePath node,
        PortName port,
        HashSet<NodePort> visiting)
    {
        NodePort key = new(node, port);
        if (!visiting.Add(key))
        {
            return StaticResolution.Missing(node, port, $"静态端口 {port} 形成环。");
        }

        try
        {
            return Find(graph, node) switch
            {
                StartNode start => ResolveStart(start, port),
                ExecutableNode executable => ResolveInput(graph, executable, port, visiting),
                ContainerNode container => ResolveOutput(graph, container, port, visiting),
                _ => StaticResolution.Missing(node, port, $"静态来源 {node} 不在流程里。"),
            };
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    /// <summary>解析来源端口：执行节点没有静态输出，其余与目标端口相同。</summary>
    private static StaticResolution ResolveSource(
        INodeGraph graph,
        NodePath node,
        PortName port,
        HashSet<NodePort> visiting)
    {
        if (Find(graph, node) is ExecutableNode executable)
        {
            return StaticResolution.Missing(executable.Path, port,
                $"执行节点 {executable.Path} 的输出端口 {port} 不是静态端口。");
        }

        return ResolveTarget(graph, node, port, visiting);
    }

    /// <summary>开始端口的来源：静态端口给固定值或注入绑定。</summary>
    private static StaticResolution ResolveStart(StartNode start, PortName port)
    {
        if (start.FindPort(port) is not { } declared)
        {
            return StaticResolution.Missing(start.Path, port, $"开始没有输出端口 {port}。");
        }

        if (!declared.IsStatic)
        {
            return StaticResolution.Missing(start.Path, port, $"开始端口 {port} 不是静态端口。");
        }

        return declared.Value is { Length: > 0 } text
            ? StaticResolution.StartFixed(start.Path, port, text)
            : StaticResolution.StartInjected(start.Path, port);
    }

    /// <summary>执行节点的静态输入：有绑定沿来源解析，没有绑定时取默认值。</summary>
    private static StaticResolution ResolveInput(
        INodeGraph graph,
        ExecutableNode executable,
        PortName port,
        HashSet<NodePort> visiting)
    {
        if (executable.FindInput(port) is not { IsStatic: true } input)
        {
            return StaticResolution.Missing(executable.Path, port,
                $"静态输出端口的目标 {port} 不是静态输入。");
        }

        if (executable.StaticBindings?.TryGetValue(port, out NodePort source) == true)
        {
            return ResolveSource(graph, source.Node, source.Port, visiting);
        }

        return input.Default is { Length: > 0 } text
            ? StaticResolution.Default(executable.Path, port, text)
            : StaticResolution.Missing(executable.Path, port,
                $"静态输入端口 {port} 没有绑定来源或默认值。");
    }

    /// <summary>容器的静态输出：必须声明为静态输出并有成员端口绑定。</summary>
    private static StaticResolution ResolveOutput(
        INodeGraph graph,
        ContainerNode container,
        PortName port,
        HashSet<NodePort> visiting)
    {
        if (container.StaticOutputs?.Contains(port) != true)
        {
            return StaticResolution.Missing(container.Path, port,
                $"容器 {container.Path} 没有静态输出端口 {port}。");
        }

        if (container.PortBindings?.TryGetValue(port, out NodePort target) != true)
        {
            return StaticResolution.Missing(container.Path, port,
                $"容器静态输出端口 {port} 没有绑定目标。");
        }

        return ResolveTarget(graph, target.Node, target.Port, visiting);
    }

    /// <summary>按路径取节点，未收录时为空。</summary>
    private static GraphNode? Find(INodeGraph graph, NodePath path) =>
        graph.Nodes.FirstOrDefault(node => node.Path == path);
}

/// <summary>静态端口解析的终点。Missing 带错误说明，其余带固定值、默认值或开始端口。</summary>
public readonly record struct StaticResolution(
    StaticSourceKind Kind,
    NodePath Node,
    PortName Port,
    string? Text,
    string? Error)
{
    /// <summary>开始端口的固定值。</summary>
    public static StaticResolution StartFixed(NodePath node, PortName port, string text) =>
        new(StaticSourceKind.StartFixed, node, port, text, null);

    /// <summary>开始端口的注入值。</summary>
    public static StaticResolution StartInjected(NodePath node, PortName port) =>
        new(StaticSourceKind.StartInjected, node, port, null, null);

    /// <summary>执行节点静态输入的默认值。</summary>
    public static StaticResolution Default(NodePath node, PortName port, string text) =>
        new(StaticSourceKind.Default, node, port, text, null);

    /// <summary>无法解析，给出错误说明。</summary>
    public static StaticResolution Missing(NodePath node, PortName port, string error) =>
        new(StaticSourceKind.Missing, node, port, null, error);
}

/// <summary>静态端口解析终点的种类。</summary>
public enum StaticSourceKind
{
    /// <summary>开始端口的固定值。</summary>
    StartFixed,

    /// <summary>开始端口的注入值。</summary>
    StartInjected,

    /// <summary>执行节点静态输入的默认值。</summary>
    Default,

    /// <summary>无法解析。</summary>
    Missing,
}