using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>静态输入端口的取值出处：沿静态来源解析到流程起点端口、常量端口或默认值。</summary>
public static class StaticInputSources
{
    /// <summary>按目标节点与输入端口给出静态来源出处，未解析时为空。</summary>
    public static ContextSource? Of(NodeGraph graph, NodePath target, PortName port) =>
        graph.StaticInputResolution(target, port) is { } resolution
            ? Map(graph, target, port, resolution)
            : null;

    /// <summary>按静态解析结果给出出处：开始端口给开始出处，常量端口给来源节点与端口，默认值给声明默认值的节点。</summary>
    private static ContextSource? Map(NodeGraph graph, NodePath target, PortName port, StaticResolution resolution) =>
        resolution.Kind switch
        {
            StaticSourceKind.StartFixed or StaticSourceKind.StartInjected => new StartSource(resolution.Port),
            StaticSourceKind.Literal or StaticSourceKind.RuntimeConstant when resolution.Source.Node is { } source =>
                graph[source] is ContainerNode
                    ? new ContainerPortSource(source, resolution.Port)
                    : new ConstantSource(source, resolution.Port),
            StaticSourceKind.Default => DefaultValueSource(resolution, target, port),
            _ => null,
        };

    /// <summary>默认值的出处：解析到上游节点时给上游节点与端口，直接取本节点声明时给目标端口。</summary>
    private static DefaultSource DefaultValueSource(StaticResolution resolution, NodePath target, PortName port) =>
        resolution.Source.Node is { } node
            ? new DefaultSource(node, resolution.Port)
            : new DefaultSource(target, port);
}