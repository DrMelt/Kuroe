using ErrorOr;

namespace Kuroe.Executions.Runs;

/// <summary>执行一次 run 的一轮请求。替换它即可在不接模型的情况下验证流程推进。</summary>
public interface IRunExecutor
{
    /// <summary>执行一轮请求，返回完整回复或错误。</summary>
    Task<ErrorOr<string>> ExecuteAsync(Run run, CancellationToken cancellationToken);
}

