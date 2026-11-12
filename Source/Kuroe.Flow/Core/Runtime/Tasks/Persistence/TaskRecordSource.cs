using Kuroe.Flow.Persistence.Saving.Shared;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>按任务号从任务域捕获存档。任务状态变化时转发任务号。</summary>
public sealed class TaskRecordSource : ITaskRecordSource
{
    private readonly TaskRegistry _registry;

    /// <summary>按任务域建立捕获源，并接上任务状态变化通知。</summary>
    public TaskRecordSource(TaskRegistry registry)
    {
        _registry = registry;
        registry.Changed += OnChanged;
    }

    /// <inheritdoc/>
    public event Action<TaskId>? Changed;

    /// <inheritdoc/>
    public TaskRecordCaptureResult Capture(TaskId id)
    {
        if (_registry.Find(id) is not { IsError: false } found)
        {
            return new TaskRecordCaptureResult(false, default);
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            TaskRecordCapture capture = new(task);

            return new TaskRecordCaptureResult(true, capture.CaptureRecord());
        }
    }

    private void OnChanged(TaskId id) => Changed?.Invoke(id);
}