using ErrorOr;
using Kuroe.FlowDescription.Shared.Model;
using Kuroe.FlowDescription.Shared.StaticGraph.Graph;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>绑定来源到执行步骤端口的解析：容器来源沿输出映射下钻到成员，开始端口保持为开始来源。</summary>
internal static class MafSourceResolution
{
    /// <summary>解析绑定来源。</summary>
    internal static ErrorOr<MafSource> Resolve(NodeGraph graph, BindingSource source)
    {
        if (source.IsStart)
        {
            return source.Port is { } port
                ? new MafSource(null, port)
                : Error.Validation(FlowErrorCodes.FlowComposition, $"开始来源 {source.Display} 没有端口。");
        }

        if (source.Node is { } node)
        {
            return ResolveNode(graph, node, source.Port);
        }

        return Error.Validation(FlowErrorCodes.FlowComposition,
            $"来源 {source.Display} 既不是开始端口也不是节点。");
    }

    /// <summary>解析节点来源：执行节点直接给出，容器沿输出映射下钻。</summary>
    private static ErrorOr<MafSource> ResolveNode(NodeGraph graph, NodePath node, PortName? port) =>
        graph[node] switch
        {
            ExecutableNode executable => new MafSource(executable.Path, port),
            ContainerNode container => ResolveContainer(graph, container, port),
            _ => Error.Validation(FlowErrorCodes.FlowComposition, $"来源节点 {node} 不是可执行节点。"),
        };

    /// <summary>解析容器来源：没有端口时取唯一执行成员，有端口时沿容器的输出映射或输入来源下钻。</summary>
    private static ErrorOr<MafSource> ResolveContainer(
        NodeGraph graph,
        ContainerNode container,
        PortName? port)
    {
        if (port is not { } name)
        {
            IReadOnlyList<NodePath> members = graph.ExecutablesIn(container.Path);

            return members.Count == 1
                ? new MafSource(members[0], null)
                : Error.Validation(FlowErrorCodes.FlowComposition,
                    $"容器 {container.Path} 有 {members.Count} 个执行成员，MAF 步骤不支持成员汇合。");
        }

        if (container.OutputBindings?.TryGetValue(name, out OutputBinding binding) == true)
        {
            return binding.Source.Equals(container.Path)
                ? FromContainerInput(graph, container, binding.Port)
                : ResolveNode(graph, binding.Source, binding.Port);
        }

        return Error.Validation(FlowErrorCodes.FlowComposition,
            $"容器 {container.Path} 没有输出端口 {name} 的来源映射。");
    }

    /// <summary>容器输出转发自身输入：沿输入的来源解析。</summary>
    private static ErrorOr<MafSource> FromContainerInput(
        NodeGraph graph,
        ContainerNode container,
        PortName port)
    {
        if (container.InputBindings is { } bindings
            && bindings.TryGetValue(port, out InputBinding? binding))
        {
            return Resolve(graph, binding.Source);
        }

        return Error.Validation(FlowErrorCodes.FlowComposition,
            $"容器 {container.Path} 的输入 {port} 没有外部来源。");
    }
}

/// <summary>绑定来源的解析结果：执行节点端口或流程开始端口。</summary>
internal readonly record struct MafSource(NodePath? Node, PortName? Port)
{
    /// <summary>是否来自流程开始端口。</summary>
    internal bool IsStart => Node is null && Port is not null;
}