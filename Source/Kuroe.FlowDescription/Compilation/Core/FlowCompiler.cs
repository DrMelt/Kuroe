using ErrorOr;
using Kuroe.FlowDescription.Compilation.Graph;
using Kuroe.FlowDescription.Compilation.Validation;
using Kuroe.FlowDescription.Shared.StaticGraph.Flow;
using Kuroe.FlowDescription.Shared.StaticGraph.Graph;
using Kuroe.FlowDescription.Shared.StaticGraph.Validation;

namespace Kuroe.FlowDescription.Compilation;

/// <summary>单条流程的编译顺序：流程规则校验、节点树到执行视图的编译、图层规则校验。</summary>
internal static class FlowCompiler
{
    /// <summary>校验并编译一条流程。流程规则不通过时不再编译，编译失败时不再校验图规则。</summary>
    public static ErrorOr<NodeGraph> Compile(FlowDefinition flow)
    {
        List<Error> errors = [];
        FlowRules.Validate(flow, errors);
        if (errors.Count > 0)
        {
            return errors;
        }

        ErrorOr<NodeGraph> compiled = FlowGraphCompiler.Compile(flow);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        GraphRules.Validate(compiled.Value, NodeScope.Flow(flow.Name.Value), errors);

        return errors.Count > 0 ? errors : compiled.Value;
    }
}