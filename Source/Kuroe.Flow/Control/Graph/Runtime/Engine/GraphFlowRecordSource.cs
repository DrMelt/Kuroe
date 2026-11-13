using Kuroe.Flow.Control.Graph.Runtime.Graph;
using Kuroe.Flow.Control.Shared;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Flow.Node.Shared.Graph.Records;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Control.Graph.Runtime.Engine;

/// <summary>图流程的存档捕获：流程定义与节点、端口状态。</summary>
public sealed class GraphFlowRecordSource : IFlowRecordSource
{
    /// <inheritdoc/>
    public FlowRecord CaptureRecord(ITaskFlowContext task)
    {
        if (task.FlowState is not GraphFlowState state || state.Definition is not { } definition)
        {
            throw new InvalidOperationException("图流程任务没有流程快照，不能写存档。");
        }

        TaskRuntime runtime = state.Runtime;
        FlowSeed seed = task.Control?.CaptureSeed() ?? FlowSeed.Empty;

        return new FlowRecord(
            new FlowIdentity(definition.Name, FlowRecordKinds.Graph, GraphFlowIdentity.Implementation),
            definition,
            new GraphFlowStateRecord(
                new TaskStartSnapshot(state.Inputs, state.StartTexts.Texts),
                [.. runtime.Materializer.Materialized
                    .Select(node => ((INodeRecordSource)node).CaptureRecord())
                    .OfType<NodeRecord>()],
                [.. runtime.Materializer.Materialized
                    .SelectMany(node => ((IPortRecordSource)node).CapturePorts())],
                seed));
    }
}