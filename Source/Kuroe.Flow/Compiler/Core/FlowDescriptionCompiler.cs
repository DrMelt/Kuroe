using ErrorOr;
using Kuroe.Flow.Compiler.Compilation;
using Kuroe.Flow.Compiler.Description;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Compiler.Validation;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.Compiler;

/// <summary>流程描述的统一编译入口：构造、展开、校验与图编译按固定顺序执行。</summary>
public sealed class FlowDescriptionCompiler(
    INodeTypeDescriptionRegistry types,
    IFlowExpander expander) : IFlowDescriptionCompiler
{
    /// <summary>编译一份描述文档。任一条流程失败时返回全部错误。</summary>
    ErrorOr<CompiledDocument> IFlowDescriptionCompiler.Compile(
        FlowDocument document,
        IReadOnlyList<NodeSpec> builtins)
    {
        FlowModelBuilder builder = new(types);
        ErrorOr<IReadOnlyList<FlowDefinition>> authoring = builder.Build(document);
        ErrorOr<IReadOnlyList<NodeSpec>> fileNodes = builder.BuildLibrary(document.Nodes);
        List<Error> errors = [];
        if (authoring.IsError)
        {
            errors.AddRange(authoring.ErrorsOrEmptyList);
        }

        if (fileNodes.IsError)
        {
            errors.AddRange(fileNodes.ErrorsOrEmptyList);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        errors.AddRange(Duplicated(authoring.Value));
        if (errors.Count > 0)
        {
            return errors;
        }

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = expander.Expand(authoring.Value, builtins, fileNodes.Value);
        if (expanded.IsError)
        {
            return expanded.ErrorsOrEmptyList;
        }

        List<CompiledFlow> flows = [];
        foreach (FlowDefinition flow in expanded.Value)
        {
            ErrorOr<NodeGraph> compiled = FlowRules.ValidateAndCompile(flow);
            if (compiled.IsError)
            {
                errors.AddRange(compiled.ErrorsOrEmptyList);
                continue;
            }

            flows.Add(new CompiledFlow(flow, compiled.Value));
        }

        return errors.Count > 0 ? errors : new CompiledDocument(flows);
    }

    /// <summary>流程名重复：同一文档内名字是流程的唯一标识。</summary>
    private static IEnumerable<Error> Duplicated(IReadOnlyList<FlowDefinition> flows)
    {
        HashSet<FlowName> seen = [];
        foreach (FlowDefinition flow in flows.Where(flow => !seen.Add(flow.Name)))
        {
            yield return FlowErrors.Body(flow.Name.Value, "流程名重复。");
        }
    }
}