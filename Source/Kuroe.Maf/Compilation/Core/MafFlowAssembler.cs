using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Maf.Compilation.Projection;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation;

/// <summary>MAF 流程装配：文档编译后逐流程投影成 MAF 流程定义，错误一次给全。</summary>
public sealed class MafFlowAssembler(
    IFlowDescriptionCompiler compiler,
    IMafNodeExecutions executions) : IMafFlowAssembler
{
    /// <inheritdoc/>
    public ErrorOr<IReadOnlyList<MafFlowDefinition>> Assemble(
        FlowDescriptionSet document,
        IReadOnlyList<NodeSpec> builtins)
    {
        ErrorOr<IReadOnlyList<FlowDefinition>> compiled = compiler.Compile(document, builtins);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        List<MafFlowDefinition> flows = [];
        List<Error> errors = [];
        foreach (FlowDefinition flow in compiled.Value)
        {
            ErrorOr<MafFlowDefinition> projected = MafFlowProjection.Project(flow, executions);
            if (projected.IsError)
            {
                errors.AddRange(projected.ErrorsOrEmptyList);
                continue;
            }

            flows.Add(projected.Value);
        }

        return errors.Count > 0 ? errors : flows;
    }
}