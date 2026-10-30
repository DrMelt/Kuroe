using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>按任务号从任务域捕获检查点。任务完成时转发任务号。</summary>
public sealed class TaskCheckpointSource : ITaskCheckpointSource
{
    private readonly TaskRegistry _registry;

    /// <summary>按任务域建立捕获源，并接上任务完成通知。</summary>
    public TaskCheckpointSource(TaskRegistry registry)
    {
        _registry = registry;
        registry.Completed += OnCompleted;
    }

    /// <inheritdoc/>
    public event Action<TaskId>? Completed;

    /// <inheritdoc/>
    public TaskCheckpointCaptureResult Capture(TaskId id)
    {
        if (_registry.Find(id) is not { IsError: false } found)
        {
            return new TaskCheckpointCaptureResult(false, default);
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            return new TaskCheckpointCaptureResult(
                true,
                new TaskCheckpointCapture(task).CaptureCheckpoint(task.CaptureSeed()));
        }
    }

    private void OnCompleted(WorkTask task) => Completed?.Invoke(task.Id);
}
