using ErrorOr;
using Kuroe.FlowDescription.Compilation.Description;
using Kuroe.FlowDescription.Compilation.Validation;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.FlowDescription.Compilation.Shared.Flow;
using Kuroe.FlowDescription.Compilation.Shared.NodeTypes;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Tasks.Shared;

namespace Kuroe.FlowDescription.Compilation;

/// <summary>流程描述的统一编译入口：构造、展开、校验与图编译按固定顺序执行。</summary>
public sealed class FlowDescriptionCompiler(
    INodeTypeDescriptionRegistry types,
    IFlowExpander expander) : IFlowDescriptionCompiler
{
    /// <summary>编译一份流程描述。任一条流程失败时返回全部错误。</summary>
    ErrorOr<IReadOnlyList<FlowDefinition>> IFlowDescriptionCompiler.Compile(
        FlowDescriptionSet descriptionSet,
        IReadOnlyList<NodeSpec> builtins)
    {
        FlowModelBuilder builder = new(types);
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

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = expander.Expand(authoring.Value, builtins, fileNodes.Value);
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
