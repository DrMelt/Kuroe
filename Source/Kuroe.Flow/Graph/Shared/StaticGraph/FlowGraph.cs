using Kuroe.FlowDescription.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Graph.Shared.StaticGraph;

/// <summary>流程定义与它的节点图：校验通过的流程定义与由它编译出的执行视图。</summary>
/// <param name="definition">校验通过的流程定义。</param>
/// <param name="graph">流程的执行视图。</param>
public sealed class FlowGraph(FlowDefinition definition, NodeGraph graph)
{
    /// <summary>校验通过的流程定义。</summary>
    public FlowDefinition Definition { get; } = definition;

    /// <summary>流程的执行视图。</summary>
    public NodeGraph Graph { get; } = graph;
}