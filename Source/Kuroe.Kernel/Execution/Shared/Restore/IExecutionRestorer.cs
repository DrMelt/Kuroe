using ErrorOr;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Shared.Restore;

/// <summary>执行目标的恢复能力。能否恢复、负载校验与恢复运行器的建立由目标类型决定。</summary>
public interface IExecutionRestorer
{
    /// <summary>校验恢复负载并装配一次恢复。不能恢复时返回错误。</summary>
    ErrorOr<IExecutionRestoreData> Prepare(ExecutionRestoreContext context);

    /// <summary>按恢复结果建立运行器。</summary>
    IExecutionRunner CreateRestored(ExecutionRestoreContext context, IExecutionRestoreData data);
}