using Kuroe.FlowDescription.Description.Shared.Reporting;
using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Compilation.Validation;
using Kuroe.FlowDescription.Definition.Shared.Flow;

namespace Kuroe.Flow.Compilation;

/// <summary>单条流程的图编译：建立执行视图并执行图级规则。</summary>
public static class FlowCompiler
{
    /// <summary>编译一条展开后的流程定义。</summary>
    public static ErrorOr<NodeGraph> Compile(FlowDefinition flow)
    {
        List<Error> errors = [];
        FlowRules.Validate(flow, errors);
        if (errors.Count > 0)
        {
            return errors;
        }

        ErrorOr<NodeGraph> compiled = NodeGraphCompiler.Compile(flow);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        GraphRules.Validate(compiled.Value, NodeScope.Flow(flow.Name.Value), errors);

        return errors.Count > 0 ? errors : compiled.Value;
    }
}
