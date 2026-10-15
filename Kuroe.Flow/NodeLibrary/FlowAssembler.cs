using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;

namespace Kuroe.Flow.NodeLibrary;

/// <summary>流程的装配：把模型内容校验并展开。文件形状的读写与转换由文件层扩展承担。</summary>
static class FlowAssembler
{
    /// <summary>校验并展开模型内容：文件节点并入节点库副本后逐流程展开，错误一次给全，流程名重复一并报出。
    /// 文件内节点重名直接报错，结果里的节点库是叠加文件定义后的生效库。</summary>
    public static ErrorOr<AssembledFlows> Assemble(
        IReadOnlyList<NodeSpec> fileNodes,
        IReadOnlyList<FlowDefinition> flows,
        NodeLibrary library,
        NodeTypeRegistry types)
    {
        NodeLibrary effective = library.Copy();
        List<Error> errors = [.. effective.Register(fileNodes)
            .Select(name => NodeScope.Library.Node(name.Value, "节点名重复。"))];
        if (errors.Count > 0)
        {
            return errors;
        }

        errors.AddRange(Validate(effective, flows, types, out List<FlowDefinition> valid));
        if (FlowRules.Duplicated(valid) is { } repeated)
        {
            errors.Add(FlowErrors.Body(repeated.Value, "流程名重复。"));
        }

        return errors.Count > 0 ? errors : new AssembledFlows(effective, fileNodes, flows, valid);
    }

    /// <summary>校验节点库，合法时逐流程展开校验。节点库不合法时只给节点库错误，合法流程经 valid 交回。</summary>
    private static List<Error> Validate(
        NodeLibrary library,
        IReadOnlyList<FlowDefinition> flows,
        NodeTypeRegistry types,
        out List<FlowDefinition> valid)
    {
        ErrorOr<Success> libraryChecked = NodeLibraryRules.Validate(library, types);
        if (libraryChecked.IsError)
        {
            valid = [];

            return [.. libraryChecked.ErrorsOrEmptyList];
        }

        List<Error> errors = [];
        List<FlowDefinition> accepted = [];
        foreach (FlowDefinition flow in flows)
        {
            ErrorOr<NodeSpec> expanded = NodeExpander.Expand(library, flow.RootNode, NodeScope.Flow(flow.Name.Value));
            if (expanded.IsError)
            {
                errors.AddRange(expanded.ErrorsOrEmptyList);
                continue;
            }

            FlowDefinition concrete = new(flow.Name, flow.Description, flow.Models, flow.StartOutputs, expanded.Value);
            ErrorOr<Success> checkedFlow = FlowRules.Validate(concrete, types);
            if (checkedFlow.IsError)
            {
                errors.AddRange(checkedFlow.ErrorsOrEmptyList);
            }
            else
            {
                accepted.Add(concrete);
            }
        }

        valid = accepted;

        return errors;
    }
}

/// <summary>一次装配的结果：叠加文件定义后的生效节点库、文件来源的节点与流程模型，以及展开后的流程。</summary>
internal sealed record AssembledFlows(
    NodeLibrary Library,
    IReadOnlyList<NodeSpec> FileNodes,
    IReadOnlyList<FlowDefinition> FlowModels,
    IReadOnlyList<FlowDefinition> Flows);
