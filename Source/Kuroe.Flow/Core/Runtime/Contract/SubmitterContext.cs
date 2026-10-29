using ErrorOr;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>契约提交入口的提交者认定：执行必须仍在跑，并把执行与它的归属任务一并交出。</summary>
internal static class SubmitterContext
{
    /// <summary>认定提交者，action 是提交入口的动作名，进入未在跑的提示。</summary>
    public static ErrorOr<(Execution Execution, WorkTask Task)> Resolve(ExecutionIndex executions, ExecutionId id, string action)
    {
        if (executions.FindOwned(id) is not { IsError: false } found || !found.Value.Execution.IsLive)
        {
            return Error.Validation("Tool.Argument", $"只有进行中的执行能{action}。");
        }

        return (found.Value.Execution, found.Value.Owner);
    }
}
