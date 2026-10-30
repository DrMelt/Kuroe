using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>启动加载：读取检查点、编译流程快照并恢复任务。</summary>
public interface ITaskRestoreService
{
    /// <summary>加载全部可恢复任务。单个文件失败不阻止其他任务。</summary>
    TaskLoadReport Restore();
}

/// <summary>一次启动加载的报告。</summary>
public sealed record TaskLoadReport(
    IReadOnlyList<TaskSnapshot> Loaded,
    IReadOnlyList<TaskId> Busy,
    IReadOnlyList<TaskLoadFailure> Failures);

/// <summary>一个任务检查点的加载失败。</summary>
public sealed record TaskLoadFailure(
    string Source,
    IReadOnlyList<Error> Errors);
