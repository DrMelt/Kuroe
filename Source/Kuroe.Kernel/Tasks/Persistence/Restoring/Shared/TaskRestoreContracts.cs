using ErrorOr;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Tasks.Persistence.Restoring.Shared;

/// <summary>启动加载：读取存档、编译流程快照并恢复任务。</summary>
public interface ITaskRestoreService
{
    /// <summary>加载全部可恢复任务。单个文件失败不阻止其他任务。</summary>
    TaskLoadReport Restore();
}

/// <summary>一次启动加载的报告。</summary>
public sealed record TaskLoadReport(
    IReadOnlyList<TaskSnapshot> Loaded,
    IReadOnlyList<TaskId> Busy,
    IReadOnlyList<TaskLoadFailure> Failures)
{
    /// <summary>合并同一进程内多个任务域的加载报告。</summary>
    public TaskLoadReport Merge(TaskLoadReport other) =>
        new(
            [.. Loaded, .. other.Loaded],
            [.. Busy, .. other.Busy],
            [.. Failures, .. other.Failures]);
}

/// <summary>一个任务存档的加载失败。文件级失败没有任务键。</summary>
public sealed record TaskLoadFailure(
    TaskId? Task,
    string Source,
    IReadOnlyList<Error> Errors);
