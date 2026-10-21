using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>契约提交入口的提交者认定：执行必须仍在跑，并把执行与它的归属任务一并交出。</summary>
internal static class SubmitterContext
{
    /// <summary>认定提交者，action 是提交入口的动作名，进入未在跑的提示。</summary>
    public static ErrorOr<(Execution Execution, WorkTask Task)> Resolve(TaskRegistry registry, ExecutionId id, string action)
    {
        if (registry.FindExecution(id) is not { IsError: false } found || !found.Value.IsLive)
        {
            return Error.Validation("Tool.Argument", $"只有进行中的执行能{action}。");
        }

        ErrorOr<WorkTask> task = registry.Find(found.Value.Input.Task);
        if (task.IsError)
        {
            return Error.Unexpected("Tool.Internal", "该执行没有归属任务。");
        }

        return (found.Value, task.Value);
    }
}
