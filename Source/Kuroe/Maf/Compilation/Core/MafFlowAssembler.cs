using ErrorOr;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Semantics.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Maf.Compilation.Projection;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation;

/// <summary>MAF 流程装配：文档编译与语言解析后逐流程投影成 MAF 流程定义，错误一次给全。</summary>
public sealed class MafFlowAssembler(
    IFlowSemanticCompiler compiler,
    IMafNodeExecutions executions) : IMafFlowAssembler
{
    /// <inheritdoc/>
    public ErrorOr<IReadOnlyList<MafFlowDefinition>> Assemble(FlowDescriptionSet document)
    {
        ErrorOr<IReadOnlyList<FlowSemantics>> compiled = compiler.CompileWithSemantics(document);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        List<MafFlowDefinition> flows = [];
        List<Error> errors = [];
        foreach (FlowSemantics compilation in compiled.Value)
        {
            if (compilation.Diagnostics.Count > 0)
            {
                errors.AddRange(compilation.Diagnostics.Select(diagnostic =>
                    FlowErrors.Body(compilation.Definition.Name.Value, diagnostic.Message)));
                continue;
            }

            ErrorOr<MafFlowDefinition> projected = MafFlowProjection.Project(compilation, executions);
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