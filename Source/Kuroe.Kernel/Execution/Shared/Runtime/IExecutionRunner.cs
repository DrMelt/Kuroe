using ErrorOr;
using Kuroe.Execution.Shared.Journal;
using Kuroe.Execution.Shared.Ports;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>一次执行运行器。运行器只读取执行请求入口与自身执行参数，不读取节点与图。</summary>
public interface IExecutionRunner
{
    /// <summary>本次执行的过程记录，执行开始前即可读取。</summary>
    IProcessJournal Journal { get; }

    /// <summary>执行一轮，只报成功或失败，成功时给出本次产出。</summary>
    Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        CancellationToken cancellationToken);
}