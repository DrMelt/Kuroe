using Kuroe.FlowDescription.Description.Shared.Reporting;
using ErrorOr;
using Kuroe.FlowDescription.Compilation.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;

namespace Kuroe.FlowDescription.Compilation.Library;

/// <summary>库装配入口：校验文件节点库后逐流程展开。</summary>
public sealed class NodeLibraryExpander : IFlowExpander
{
    /// <summary>展开全部流程。节点库不合法时只给节点库错误。</summary>
    public ErrorOr<IReadOnlyList<FlowDefinition>> Expand(
        IReadOnlyList<FlowDefinition> authoringFlows,
        IReadOnlyList<NodeSpec> fileNodes)
    {
        NodeLibrary library = new();

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
            ErrorOr<NodeSpec> expanded = NodeTreeExpander.Expand(library, flow.RootNode, flow.StartPorts, NodeScope.Flow(flow.Name.Value));
            if (expanded.IsError)
            {
                errors.AddRange(expanded.ErrorsOrEmptyList);
                continue;
            }

            flows.Add(new FlowDefinition(flow.Name, flow.Description, flow.StartPorts, expanded.Value));
        }

        return errors.Count > 0 ? errors : flows;
    }
}