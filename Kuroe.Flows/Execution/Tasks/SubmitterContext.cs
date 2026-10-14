using ErrorOr;
using Kuroe.Flows.Execution.Runs;
using Kuroe.Flows.Shared.Execution;

namespace Kuroe.Flows.Execution.Tasks;

/// <summary>契约提交入口的提交者认定：run 必须仍在跑，并把 run 与它的归属任务一并交出。</summary>
internal static class SubmitterContext
{
    /// <summary>认定提交者，action 是提交入口的动作名，进入未在跑的提示。</summary>
    public static ErrorOr<(Run Run, WorkTask Task)> Resolve(TaskRegistry registry, RunId id, string action)
    {
        if (registry.FindRun(id) is not { IsError: false } found || !found.Value.IsLive)
        {
            return Error.Validation("Tool.Argument", $"只有进行中的 run 能{action}。");
        }

        ErrorOr<WorkTask> task = registry.Find(found.Value.Context.Task);
        if (task.IsError)
        {
            return Error.Unexpected("Tool.Internal", "该 run 没有归属任务。");
        }

        return (found.Value, task.Value);
    }
}