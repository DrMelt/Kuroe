using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Semantics.Shared;

namespace Kuroe.Flow.Compilation;

/// <summary>把流程定义编译成图流程的执行视图。</summary>
public interface IFlowGraphCompiler
{
    /// <summary>编译一条流程，内部完成语言结构校验与语言解析。</summary>
    ErrorOr<FlowGraph> Compile(FlowDefinition flow);

    /// <summary>按已解析的语言语义编译一条流程。</summary>
    ErrorOr<FlowGraph> Compile(FlowSemantics semantics);
}
