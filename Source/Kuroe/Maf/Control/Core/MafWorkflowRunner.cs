using Kuroe.Maf.Definition;
using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Maf.Control;

/// <summary>按流程定义启动或恢复 MAF 运行。</summary>
internal static class MafWorkflowRunner
{
    internal static Task<IMafWorkflowRun> StartAsync(
        MafFlowDefinition definition,
        Func<MafStep, MafStepNode> nodes,
        string session,
        IMafWorkflowCheckpoints checkpoints,
        ICheckpointIdSource ids,
        CancellationToken cancellationToken)
    {
        MafWorkflowGraph graph = MafWorkflow.Build(definition, nodes);
        MafWorkflowCheckpointStore store = new(checkpoints, ids);

        return store.Current is { } checkpoint
            ? ResumeAsync(graph, checkpoint, store.Manager, cancellationToken)
            : RunAsync(graph, session, store.Manager, cancellationToken);
    }

    private static async Task<IMafWorkflowRun> RunAsync(
        MafWorkflowGraph graph,
        string session,
        CheckpointManager checkpoints,
        CancellationToken cancellationToken)
    {
        StreamingRun run = await InProcessExecution
            .RunStreamingAsync(
                graph.Workflow,
                new MafStartMessage(),
                checkpoints,
                session,
                cancellationToken)
            .ConfigureAwait(false);

        return new MafWorkflowRun(run, graph.Steps, graph.StartId);
    }

    private static async Task<IMafWorkflowRun> ResumeAsync(
        MafWorkflowGraph graph,
        MafWorkflowCheckpoint checkpoint,
        CheckpointManager checkpoints,
        CancellationToken cancellationToken)
    {
        StreamingRun run = await InProcessExecution
            .ResumeStreamingAsync(
                graph.Workflow,
                new CheckpointInfo(checkpoint.Session, checkpoint.Id),
                checkpoints,
                cancellationToken)
            .ConfigureAwait(false);

        return new MafWorkflowRun(run, graph.Steps, graph.StartId);
    }
}