using ErrorOr;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>捕获任务的完整检查点。要求持有任务 Gate。</summary>
public sealed class TaskCheckpointCapture(WorkTask task) : ITaskCheckpointCapture
{
    /// <inheritdoc/>
    public ErrorOr<TaskCheckpoint> CaptureCheckpoint(SchedulerSeed seed)
    {
        lock (task.Gate)
        {
            if (task.FlowSnapshot is not { } flow)
            {
                return [Error.Failure(FlowErrorCodes.CheckpointFlowSnapshot, $"{task.Id} 没有流程快照，不能写检查点。")];
            }

            return new TaskCheckpoint(
                new TaskIdentity(task.Id, task.Title, task.CreatedAt, task.LastActivityAt),
                flow,
                new TaskStartSnapshot(task.Inputs, task.StartTexts.Texts),
                task.Lifecycle,
                task.Completed,
                new TaskJournalSnapshot(task.Journal.Entries, task.Journal.DroppedEntries),
                [.. task.Executions.Select(execution => execution.CaptureCheckpoint())],
                [.. task.Runtime.Materializer.Materialized
                    .Select(node => ((INodeCheckpointSource)node).CaptureCheckpoint())
                    .OfType<NodeCheckpoint>()],
                seed);
        }
    }
}
