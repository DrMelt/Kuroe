using Kuroe.Flow.Node.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Node.Shared.StaticGraph.Flow;

/// <summary>编译完成的流程：校验通过的具体模型与执行视图。</summary>
/// <param name="model">校验通过的流程模型。</param>
/// <param name="graph">流程的执行视图。</param>
public sealed class CompiledFlow(FlowDefinition model, NodeGraph graph)
{
    /// <summary>校验通过的具体流程模型。</summary>
    public FlowDefinition Model { get; } = model;

    /// <summary>流程的执行视图。</summary>
    public NodeGraph Graph { get; } = graph;
}