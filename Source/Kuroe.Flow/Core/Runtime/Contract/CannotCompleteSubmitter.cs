using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>模型侧上报本轮无法完成的入口：给出原因即停驻待返工。
/// 提交者身份在此认定，结果以上报后的本轮契约状态交回，回执措辞由工具实现层给出。</summary>
public sealed class CannotCompleteSubmitter(TaskRegistry registry)
{
    /// <summary>上报本轮无法完成并给出原因，原因必填。成功时给出上报后的本轮契约状态，错误以 ErrorOr 表达。</summary>
    public ErrorOr<ExecutionStatus> Report(ExecutionId id, string? reason)
    {
        ErrorOr<(Execution Execution, WorkTask Task)> owner = SubmitterContext.Resolve(registry, id, "上报无法完成");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Tool.Argument", "上报无法完成时必须给出原因。");
        }

        (Execution execution, WorkTask task) = owner.Value;
        string recorded = reason.Trim();
        lock (task.Gate)
        {
            task.Runtime.Node(execution).CollectCannotComplete(execution.Id, recorded);

            return ExecutionStatusReader.Of(task, execution);
        }
    }
}