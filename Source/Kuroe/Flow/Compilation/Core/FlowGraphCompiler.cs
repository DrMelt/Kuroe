using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Compilation.Validation;
using Kuroe.FlowDescription.Description.Shared.Reporting;
using Kuroe.FlowDescription.Semantics.Shared;

namespace Kuroe.Flow.Compilation;

/// <summary>图流程编译入口：从语言语义建立执行视图。语言规则由描述编译与语义层完成。</summary>
public sealed class FlowGraphCompiler(IFlowSemanticsAnalyzer semantics) : IFlowGraphCompiler
{
    private readonly IFlowSemanticsAnalyzer _semantics = semantics;

    /// <inheritdoc/>
    public ErrorOr<FlowGraph> Compile(FlowDefinition flow)
    {
        List<Error> errors = [];
        FlowRules.Validate(flow, errors);
        if (errors.Count > 0)
        {
            return errors;
        }

        return Compile(_semantics.Analyze(flow));
    }

    /// <inheritdoc/>
    public ErrorOr<FlowGraph> Compile(FlowSemantics semantics)
    {
        if (semantics.Diagnostics.Count > 0)
        {
            NodeScope scope = NodeScope.Flow(semantics.Definition.Name.Value);

            return [.. semantics.Diagnostics.Select(diagnostic =>
                scope.Node(diagnostic.Path.Value, diagnostic.Message))];
        }

        return new FlowGraph(semantics.Definition, NodeGraphCompiler.Compile(semantics));
    }
}
