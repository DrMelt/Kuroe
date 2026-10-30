using ErrorOr;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>任务检查点捕获契约。</summary>
public interface ITaskCheckpointCapture
{
    /// <summary>捕获任务检查点。要求持有任务 Gate。</summary>
    ErrorOr<TaskCheckpoint> CaptureCheckpoint(SchedulerSeed seed);
}
