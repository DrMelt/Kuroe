using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>模型侧声明本轮执行状态的入口：交付完毕或无法完成并给出原因。
/// 提交者身份在此认定，回执附上仍缺的产出，让它在同一轮里补齐。</summary>
public sealed class StatusSubmitter(TaskRegistry registry)
{
    /// <summary>声明本轮结论。status 取 completed 或 blocked，blocked 必须给出原因。错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> SubmitVerdict(TurnScope? scope, string? status, string? reason)
    {
        if (Owner(scope) is not { } run)
        {
            return ToolErrors.Argument("只有进行中的 run 能声明本轮状态。");
        }

        ErrorOr<RunVerdict> parsed = Parse(status, reason);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        ErrorOr<WorkTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return ToolErrors.Internal("该 run 没有归属任务。");
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            task.SetVerdict(run.Context.NodeIndex, run.Context.ItemIndex, parsed.Value);

            string recorded = parsed.Value.Blocked ? "blocked" : "completed";

            return $"已记录本轮状态：{recorded}。{ContractText.Needs(task, run)}";
        }
    }

    /// <summary>状态文本的解析：completed 与 blocked 两个取值，blocked 必须有原因。</summary>
    private static ErrorOr<RunVerdict> Parse(string? status, string? reason)
    {
        string normalized = status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized == "completed")
        {
            return RunVerdict.Done;
        }

        if (normalized != "blocked")
        {
            return ToolErrors.Argument("status 只能取 completed 或 blocked。");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ToolErrors.Argument("声明 blocked 时必须给出 reason。");
        }

        return RunVerdict.CannotComplete(reason.Trim());
    }

    /// <summary>提交者必须绑定了执行回合，且是该契约仍在跑的 run。</summary>
    private Run? Owner(TurnScope? scope) =>
        scope is { } s
        && registry.FindRun(s.Run) is { IsError: false } found
        && found.Value.IsLive
            ? found.Value
            : null;
}
