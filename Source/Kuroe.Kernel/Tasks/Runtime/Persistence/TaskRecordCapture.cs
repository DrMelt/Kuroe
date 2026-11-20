using ErrorOr;
using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Tasks.Runtime.Persistence;

/// <summary>捕获任务的完整存档。要求持有任务 Gate。</summary>
public sealed class TaskRecordCapture(WorkTask task) : ITaskRecordCapture
{
    /// <inheritdoc/>
    public ErrorOr<TaskRecord> CaptureRecord()
    {
        lock (task.Gate)
        {
            if (task.FlowSnapshot is null)
            {
                return [Error.Failure(FlowErrorCodes.RecordFlowSnapshot, $"{task.Id} 没有流程快照，不能写存档。")];
            }

            return task.CaptureRecord();
        }
    }
}

/// <summary>捕获任务的完整存档。要求持有任务 Gate。</summary>
public interface ITaskRecordCapture
{
    /// <summary>捕获任务存档。</summary>
    ErrorOr<TaskRecord> CaptureRecord();
}