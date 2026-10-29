using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime;

/// <summary>执行侧的错误构造。</summary>
public static class ExecutionErrors
{
    /// <summary>执行号不存在。</summary>
    public static Error ExecutionNotFound(int value) => Error.NotFound(FlowErrorCodes.ExecutionNotFound, $"没有执行 #{value}。");

    /// <summary>该执行已结束。</summary>
    public static Error ExecutionSettled(ExecutionId execution, string action) =>
        Error.Validation("Execution.Settled", $"{execution} 已结束，不能{action}。");
}
