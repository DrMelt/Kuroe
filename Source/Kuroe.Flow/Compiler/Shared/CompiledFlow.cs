using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Compiler.Shared;

/// <summary>编译完成的流程：校验通过的具体模型与执行视图。</summary>
/// <param name="model">校验通过的流程模型。</param>
/// <param name="graph">流程的执行视图。</param>
public sealed class CompiledFlow(FlowDefinition model, INodeGraph graph)
{
    /// <summary>校验通过的具体流程模型。</summary>
    public FlowDefinition Model { get; } = model;

    /// <summary>流程的执行视图。</summary>
    public INodeGraph Graph { get; } = graph;
}

/// <summary>一次文档编译的结果：全部流程已校验并编译。</summary>
/// <param name="flows">编译完成的流程，按文档声明序。</param>
public sealed class CompiledDocument(IReadOnlyList<CompiledFlow> flows)
{
    /// <summary>编译完成的流程，按文档声明序。</summary>
    public IReadOnlyList<CompiledFlow> Flows { get; } = flows;
}