using Kuroe.Flow.Control.Runtime.Graph;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Flow.Records.Shared;
using Kuroe.Flow.Graph.Shared.Graph.Records;
using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Control.Runtime.Engine;

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
            new FlowIdentity(definition.Name, GraphFlowKind.Value, GraphFlowIdentity.Implementation),
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