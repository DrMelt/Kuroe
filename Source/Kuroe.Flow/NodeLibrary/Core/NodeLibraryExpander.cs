using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.NodeLibrary;

/// <summary>库装配入口：合并内置与文件定义，校验节点库后逐流程展开。</summary>
public sealed class NodeLibraryExpander : IFlowExpander
{
    /// <summary>展开全部流程。节点库不合法时只给节点库错误。</summary>
    public ErrorOr<IReadOnlyList<FlowDefinition>> Expand(
        IReadOnlyList<FlowDefinition> authoringFlows,
        IReadOnlyList<NodeSpec> builtins,
        IReadOnlyList<NodeSpec> fileNodes)
    {
        NodeLibrary library = new();
        library.Register(builtins);

        List<Error> errors = [.. library.Register(fileNodes)
            .Select(name => NodeScope.Library.Node(name.Value, "节点名重复。"))];
        if (errors.Count > 0)
        {
            return errors;
        }

        ErrorOr<Success> checkedLibrary = NodeLibraryRules.Validate(library);
        if (checkedLibrary.IsError)
        {
            return checkedLibrary.ErrorsOrEmptyList;
        }

        List<FlowDefinition> flows = [];
        foreach (FlowDefinition flow in authoringFlows)
        {
            ErrorOr<NodeSpec> expanded = NodeExpander.Expand(library, flow.RootNode, NodeScope.Flow(flow.Name.Value));
            if (expanded.IsError)
            {
                errors.AddRange(expanded.ErrorsOrEmptyList);
                continue;
            }

            flows.Add(new FlowDefinition(flow.Name, flow.Description, flow.StartOutputs, flow.HostStartInputs, expanded.Value));
        }

        return errors.Count > 0 ? errors : flows;
    }
}