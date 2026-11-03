using ErrorOr;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>捕获任务的完整存档。要求持有任务 Gate。</summary>
public sealed class TaskRecordCapture(WorkTask task) : ITaskRecordCapture
{
    /// <inheritdoc/>
    public ErrorOr<TaskRecord> CaptureRecord(SchedulerSeed seed)
    {
        lock (task.Gate)
        {
            if (task.FlowSnapshot is not { } flow)
            {
                return [Error.Failure(FlowErrorCodes.RecordFlowSnapshot, $"{task.Id} 没有流程快照，不能写存档。")];
            }

            return new TaskRecord(
                new TaskIdentity(task.Id, task.Title, task.CreatedAt, task.LastActivityAt),
                flow,
                new TaskStartSnapshot(task.Inputs, task.StartTexts.Texts),
                task.Lifecycle,
                task.Completed,
                new TaskJournalSnapshot(task.Journal.Entries, task.Journal.DroppedEntries),
                [.. task.Executions.Select(execution => execution.CaptureRecord())],
                [.. task.Runtime.Materializer.Materialized
                    .Select(node => ((INodeRecordSource)node).CaptureRecord())
                    .OfType<NodeRecord>()],
                seed);
        }
    }
}
