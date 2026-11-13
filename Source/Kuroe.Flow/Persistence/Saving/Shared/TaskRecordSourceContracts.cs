using ErrorOr;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Persistence.Saving.Shared;

/// <summary>按任务号捕获存档的运行时端口。由运行时实现查找、加锁与状态读取。</summary>
public interface ITaskRecordSource : ITaskChangeSource
{
    /// <summary>捕获任务存档。任务不存在时返回未找到，存在但捕获失败时返回错误。</summary>
    TaskRecordCaptureResult Capture(TaskId id);
}

/// <summary>一次存档捕获的结果。</summary>
public readonly record struct TaskRecordCaptureResult(
    bool Found,
    ErrorOr<TaskRecord> Record);
