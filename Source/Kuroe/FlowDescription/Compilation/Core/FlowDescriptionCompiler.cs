using ErrorOr;
using Kuroe.FlowDescription.Compilation.Description;
using Kuroe.FlowDescription.Compilation.Validation;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.FlowDescription.Compilation.Shared.Flow;
using Kuroe.FlowDescription.Compilation.Shared.NodeTypes;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Description.Shared.Reporting;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Semantics.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.FlowDescription.Compilation;

/// <summary>流程描述的统一编译入口：构造、展开、校验与语言解析按固定顺序执行。</summary>
/// <remarks>按类型注册表、展开器与语言解析器建立编译入口。</remarks>
public sealed class FlowDescriptionCompiler(
    INodeTypeDescriptionRegistry types,
    IFlowExpander expander,
    IFlowSemanticsAnalyzer semantics) : IFlowDescriptionCompiler, IFlowSemanticCompiler
{
    private readonly INodeTypeDescriptionRegistry _types = types;
    private readonly IFlowExpander _expander = expander;
    private readonly IFlowSemanticsAnalyzer _semantics = semantics;

    /// <summary>编译一份流程描述并给出语言解析结果。任一条流程失败时返回全部错误。</summary>
    public ErrorOr<IReadOnlyList<FlowSemantics>> CompileWithSemantics(FlowDescriptionSet descriptionSet)
    {
        ErrorOr<IReadOnlyList<FlowDefinition>> compiled = CompileDefinitions(descriptionSet);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        List<FlowSemantics> flows = [];
        List<Error> errors = [];
        foreach (FlowDefinition flow in compiled.Value)
        {
            FlowSemantics analyzed = _semantics.Analyze(flow);
            if (analyzed.Diagnostics.Count > 0)
            {
                foreach (FlowSemanticDiagnostic diagnostic in analyzed.Diagnostics)
                {
                    errors.Add(NodeScope.Flow(flow.Name.Value).Node(
                        diagnostic.Path.Value,
                        diagnostic.Message));
                }

                continue;
            }

            flows.Add(analyzed);
        }

        return errors.Count > 0 ? errors : flows;
    }

    /// <summary>编译一份流程描述，只给出语言合法的流程定义。任一条流程失败时返回全部错误。</summary>
    ErrorOr<IReadOnlyList<FlowDefinition>> IFlowDescriptionCompiler.Compile(
        FlowDescriptionSet descriptionSet)
    {
        ErrorOr<IReadOnlyList<FlowSemantics>> compiled = CompileWithSemantics(descriptionSet);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        List<FlowDefinition> definitions = [.. compiled.Value.Select(flow => flow.Definition)];

        return definitions;
    }

    private ErrorOr<IReadOnlyList<FlowDefinition>> CompileDefinitions(FlowDescriptionSet descriptionSet)
    {
        FlowModelBuilder builder = new(_types);
        ErrorOr<IReadOnlyList<FlowDefinition>> authoring = builder.Build(descriptionSet);
        ErrorOr<IReadOnlyList<NodeSpec>> fileNodes = builder.BuildLibrary(descriptionSet.Nodes);
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

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = _expander.Expand(authoring.Value, fileNodes.Value);
        if (expanded.IsError)
        {
            return expanded.ErrorsOrEmptyList;
        }

        foreach (FlowDefinition flow in expanded.Value)
        {
            FlowRules.Validate(flow, errors);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        List<FlowDefinition> flows = [.. expanded.Value];

        return flows;
    }

    /// <summary>流程名重复：同一描述内名字是流程的唯一标识。</summary>
    private static IEnumerable<Error> Duplicated(IReadOnlyList<FlowDefinition> flows)
    {
        HashSet<FlowName> seen = [];
        foreach (FlowDefinition flow in flows.Where(flow => !seen.Add(flow.Name)))
        {
            yield return FlowErrors.Body(flow.Name.Value, "流程名重复。");
        }
    }
}
