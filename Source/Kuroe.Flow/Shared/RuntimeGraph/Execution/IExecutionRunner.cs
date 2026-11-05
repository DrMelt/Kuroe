using ErrorOr;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行的运行与过程记录：过程记录由运行器持有，运行后报成功或失败并给出产出。</summary>
public interface IExecutionRunner
{
    /// <summary>本次执行的过程记录，执行开始前即可读取。</summary>
    IProcessJournal Journal { get; }

    /// <summary>执行一轮，只报成功或失败。过程记录由执行器的具体实现写入，成功时给出本次产出。</summary>
    Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionInput input,
        CancellationToken cancellationToken);
}

