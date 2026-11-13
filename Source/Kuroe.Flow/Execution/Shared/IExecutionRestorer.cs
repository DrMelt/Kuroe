using ErrorOr;

namespace Kuroe.Flow.Execution.Shared;

/// <summary>一次执行恢复的数据负载。格式由执行目标类型解释，执行层只透传。</summary>
public interface IExecutionRestoreData;

/// <summary>建立恢复计划时交给执行目标的输入：本次执行的标识、请求、目标、恢复负载与本轮契约。</summary>
public sealed record ExecutionRestoreContext(
    ExecutionId Execution,
    ExecutionRequest Request,
    IExecutionTarget Target,
    ExecutionRestoreState Payload,
    ExecutionContract Contract);

/// <summary>执行目标的恢复能力。能否恢复、负载校验与恢复运行器的建立由目标类型决定。</summary>
public interface IExecutionRestorer
{
    /// <summary>执行种类，用于选择恢复器。</summary>
    string Kind { get; }

    /// <summary>校验恢复负载并装配一次恢复。不能恢复时返回错误。</summary>
    ErrorOr<IExecutionRestoreData> Prepare(ExecutionRestoreContext context);

    /// <summary>按恢复结果建立运行器。</summary>
    IExecutionRunner CreateRestored(ExecutionRestoreContext context, IExecutionRestoreData data);
}