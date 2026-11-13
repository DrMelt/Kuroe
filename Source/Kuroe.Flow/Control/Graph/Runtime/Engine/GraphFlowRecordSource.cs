using Kuroe.Flow.Control;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Control.Graph;
using Kuroe.Flow.Control.Graph.Nodes;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Control.Graph.Engine;

/// <summary>图流程的存档捕获：流程记录与按中性执行列表生成的执行记录。</summary>
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

    /// <inheritdoc/>
    public IReadOnlyList<ExecutionRecord> CaptureExecutions(ITaskFlowContext task)
    {
        if (task.FlowState is not GraphFlowState state)
        {
            throw new InvalidOperationException("图流程存档捕获收到非图流程状态。");
        }

        return
        [
            .. task.Executions.Select(execution => new GraphExecution(
                state.Context(execution.Id).Input,
                execution,
                state.Context(execution.Id).ControlVersion).CaptureRecord()),
        ];
    }
}