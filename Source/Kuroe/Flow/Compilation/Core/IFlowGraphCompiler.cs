using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Definition.Shared.Flow;

namespace Kuroe.Flow.Compilation;

/// <summary>把展开后的流程定义编译成图流程的执行视图。</summary>
public interface IFlowGraphCompiler
{
    /// <summary>编译一条流程，语言规则已通过。</summary>
    ErrorOr<FlowGraph> Compile(FlowDefinition flow);
}
