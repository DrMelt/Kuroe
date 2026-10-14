using ErrorOr;
using Kuroe.Flows.Execution.Runs;
using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.RuntimeGraph;

namespace Kuroe.Flows.Execution.Tasks;

/// <summary>模型侧声明本轮执行状态的入口：交付完毕或无法完成并给出原因。
/// 提交者身份在此认定，回执附上仍缺的产出，让它在同一轮里补齐。</summary>
public sealed class StatusSubmitter(TaskRegistry registry)
{
    /// <summary>声明本轮结论。status 取 completed 或 blocked，blocked 必须给出原因。错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> SubmitVerdict(RunId id, string? status, string? reason)
    {
        ErrorOr<(Run Run, WorkTask Task)> owner = SubmitterContext.Resolve(registry, id, "声明本轮状态");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        ErrorOr<RunVerdict> parsed = Parse(status, reason);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        (Run run, WorkTask task) = owner.Value;
        lock (task.Gate)
        {
            task.Runtime.Instance(run).CollectVerdict(run.Id, parsed.Value);

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
            return Error.Validation("Tool.Argument", "status 只能取 completed 或 blocked。");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Tool.Argument", "声明 blocked 时必须给出 reason。");
        }

        return RunVerdict.CannotComplete(reason.Trim());
    }
}
