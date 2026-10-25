using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>任务工作目录的准备：宿主按任务号、流程与标题建立目录，建立失败时任务不启动。</summary>
public interface ITaskWorkspace
{
    /// <summary>准备任务目录并给出它的绝对路径。名字已被占用时给出 <see cref="FlowErrorCodes.TaskWorkspaceConflict"/>，提交方换号重试。</summary>
    /// <param name="id">任务标识。</param>
    /// <param name="flow">任务的流程名。</param>
    /// <param name="title">任务标题。</param>
    ErrorOr<string> Prepare(TaskId id, FlowName flow, string title);
}