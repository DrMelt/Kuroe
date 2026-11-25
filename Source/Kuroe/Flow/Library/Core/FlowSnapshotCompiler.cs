using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.Flow.Records.Shared;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Json.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Library;

/// <summary>把存档快照重新编译为可运行流程，不依赖流程库状态。</summary>
public sealed class FlowSnapshotCompiler(
    INodeFormatReader nodes,
    IFlowFormatReader flows,
    IFlowCompiler compiler) : IFlowSnapshotCompiler
{
    /// <inheritdoc/>
    public ErrorOr<FlowGraph> Compile(GraphFlowDefinitionRecord snapshot)
    {
        ErrorOr<IReadOnlyList<NodeDefinitionDescription>> nodeList = nodes.Read(snapshot.Nodes);
        if (nodeList.IsError)
        {
            return [Error.Failure(
                FlowErrorCodes.RecordFlowSnapshot,
                $"流程快照的节点库不合法：{nodeList.FirstError.Description}")];
        }

        ErrorOr<IReadOnlyList<FlowEntry>> flowList = flows.Read(snapshot.Flow);
        if (flowList.IsError)
        {
            return [Error.Failure(
                FlowErrorCodes.RecordFlowSnapshot,
                $"流程快照的流程不合法：{flowList.FirstError.Description}")];
        }

        ErrorOr<FlowSetCompilation.CompileResult> compiled =
            FlowSetCompilation.Compile(new FlowDescriptionSet(nodeList.Value, flowList.Value), compiler);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        FlowGraph? flow = compiled.Value.Flows.FirstOrDefault(candidate => candidate.Definition.Name == snapshot.Name);

        return flow is null
            ? [Error.Failure(FlowErrorCodes.RecordFlowSnapshot, $"流程快照中没有流程 {snapshot.Name}。")]
            : flow;
    }
}
