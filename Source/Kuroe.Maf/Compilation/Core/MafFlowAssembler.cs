using ErrorOr;
using Kuroe.Flow.Compilation.Documents.Shared;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Node.Shared.StaticGraph.Flow;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Maf.Compilation.Projection;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation;

/// <summary>MAF 流程装配：文档编译后逐流程投影成 MAF 流程定义，错误一次给全。</summary>
public sealed class MafFlowAssembler(IFlowDescriptionCompiler compiler) : IMafFlowAssembler
{
    /// <inheritdoc/>
    public ErrorOr<IReadOnlyList<MafFlowDefinition>> Assemble(
        FlowDocument document,
        IReadOnlyList<NodeSpec> builtins)
    {
        ErrorOr<IReadOnlyList<FlowGraph>> compiled = compiler.Compile(document, builtins);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        List<MafFlowDefinition> flows = [];
        List<Error> errors = [];
        foreach (FlowGraph flow in compiled.Value)
        {
            ErrorOr<MafFlowDefinition> projected = MafFlowProjection.Project(flow);
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