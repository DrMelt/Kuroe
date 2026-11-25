using ErrorOr;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.FlowDescription.Definition.Shared.Flow;

namespace Kuroe.Flow.Library;

/// <summary>图流程编译：描述编译后逐流程建立执行视图。</summary>
public interface IFlowCompiler
{
    /// <summary>编译一份流程描述为图流程。</summary>
    ErrorOr<IReadOnlyList<FlowGraph>> Compile(FlowDescriptionSet descriptionSet);
}

/// <summary>描述编译与图编译的组合。</summary>
public sealed class FlowCompilation(
    IFlowDescriptionCompiler descriptions,
    IFlowGraphCompiler graphs) : IFlowCompiler
{
    /// <inheritdoc/>
    public ErrorOr<IReadOnlyList<FlowGraph>> Compile(FlowDescriptionSet descriptionSet)
    {
        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = descriptions.Compile(descriptionSet);
        if (expanded.IsError)
        {
            return expanded.ErrorsOrEmptyList;
        }

        List<FlowGraph> flows = [];
        List<Error> errors = [];
        foreach (FlowDefinition flow in expanded.Value)
        {
            ErrorOr<FlowGraph> compiled = graphs.Compile(flow);
            if (compiled.IsError)
            {
                errors.AddRange(compiled.ErrorsOrEmptyList);
                continue;
            }

            flows.Add(compiled.Value);
        }

        return errors.Count > 0 ? errors : flows;
    }
}
