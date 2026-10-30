using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>按任务号捕获检查点的运行时端口。由运行时实现查找、加锁与状态读取。</summary>
public interface ITaskCheckpointSource
{
    /// <summary>捕获任务检查点。任务不存在时返回未找到，存在但捕获失败时返回错误。</summary>
    TaskCheckpointCaptureResult Capture(TaskId id);

    /// <summary>任务完成时通知任务号。</summary>
    event Action<TaskId>? Completed;
}

/// <summary>一次检查点捕获的结果。</summary>
public readonly record struct TaskCheckpointCaptureResult(
    bool Found,
    ErrorOr<TaskCheckpoint> Checkpoint);
