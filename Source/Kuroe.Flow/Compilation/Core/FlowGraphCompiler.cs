using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Shared.StaticGraph.Flow;
using Kuroe.FlowDescription.Shared.StaticGraph.Validation;

namespace Kuroe.Flow.Compilation;

/// <summary>图流程编译入口：建立执行视图并执行图级规则。语言规则已由描述编译完成。</summary>
public sealed class FlowGraphCompiler : IFlowGraphCompiler
{
    /// <inheritdoc/>
    public ErrorOr<FlowGraph> Compile(FlowDefinition flow)
    {
        ErrorOr<NodeGraph> compiled = NodeGraphCompiler.Compile(flow);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        List<Error> errors = [];
        GraphRules.Validate(compiled.Value, NodeScope.Flow(flow.Name.Value), errors);

        return errors.Count > 0 ? errors : new FlowGraph(flow, compiled.Value);
    }
}
