using ErrorOr;

namespace Kuroe.Flow.Persistence.Records.Shared;

/// <summary>捕获任务的完整存档。要求持有任务 Gate。</summary>
public interface ITaskRecordCapture
{
    /// <summary>按调度种子捕获任务存档。</summary>
    ErrorOr<TaskRecord> CaptureRecord(SchedulerSeed seed);
}
