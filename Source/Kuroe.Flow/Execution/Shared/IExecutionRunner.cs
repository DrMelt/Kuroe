using ErrorOr;

namespace Kuroe.Flow.Execution.Shared;

/// <summary>一次执行运行器。运行器只读取执行请求与执行目标，不读取节点与图。</summary>
public interface IExecutionRunner
{
    /// <summary>本次执行的过程记录，执行开始前即可读取。</summary>
    IProcessJournal Journal { get; }

    /// <summary>执行一轮，只报成功或失败，成功时给出本次产出。</summary>
    Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken);
}