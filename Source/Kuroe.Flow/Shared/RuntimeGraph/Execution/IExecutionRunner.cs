using ErrorOr;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行的运行与过程记录：过程记录由运行器持有，运行后只报成功或失败。</summary>
public interface IExecutionRunner
{
    /// <summary>本次执行的过程记录，执行开始前即可读取。</summary>
    IProcessJournal Journal { get; }

    /// <summary>执行一轮请求，只报成功或失败。过程记录由执行器的具体实现写入。</summary>
    Task<ErrorOr<Success>> ExecuteAsync(
        ExecutionInput input,
        CancellationToken cancellationToken);
}

