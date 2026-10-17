using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Flow.Execution.Runs;

/// <summary>run 侧的错误构造。</summary>
public static class RunErrors
{
    /// <summary>run 号不存在。</summary>
    public static Error RunNotFound(int value) => Error.NotFound(FlowErrorCodes.RunNotFound, $"没有 run #{value}。");

    /// <summary>该 run 已结束。</summary>
    public static Error RunSettled(RunId run, string action) =>
        Error.Validation("Run.Settled", $"{run} 已结束，不能{action}。");
}
