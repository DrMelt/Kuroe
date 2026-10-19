using ErrorOr;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>执行的创建与运行：先创建本次执行的节点侧执行体，再跑一轮请求。
/// 替换它即可在不接模型的情况下验证流程推进。</summary>
public interface IExecutionRunner
{
    /// <summary>为一次执行创建节点侧的执行体，其中含实现过程记录的对象。</summary>
    IExecutionBody CreateBody(ExecutionInput input);

    /// <summary>执行一轮请求，只报成功或失败。文本增量与工具调用经执行的过程播发送出，过程记录由执行体自身写入。</summary>
    Task<ErrorOr<Success>> ExecuteAsync(IExecution execution, IExecutionBody body, CancellationToken cancellationToken);
}

