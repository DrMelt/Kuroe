using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Graph.Records;

/// <summary>捕获任务的完整存档。要求持有任务 Gate。</summary>
public interface ITaskRecordCapture
{
    /// <summary>按调度种子捕获任务存档。</summary>
    ErrorOr<TaskRecord> CaptureRecord(SchedulerSeed seed);
}
