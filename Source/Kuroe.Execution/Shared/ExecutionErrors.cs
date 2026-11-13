using ErrorOr;

namespace Kuroe.Execution.Shared;

/// <summary>执行侧的错误构造。</summary>
public static class ExecutionErrors
{
    /// <summary>执行号不存在。</summary>
    public static Error ExecutionNotFound(int value) => Error.NotFound(ExecutionErrorCodes.NotFound, $"没有执行 #{value}。");

    /// <summary>该执行已结束。</summary>
    public static Error ExecutionSettled(ExecutionId execution, string action) =>
        Error.Validation("Execution.Settled", $"{execution} 已结束，不能{action}。");

    /// <summary>该执行尚未启动。</summary>
    public static Error ExecutionNotStarted(ExecutionId execution) =>
        Error.Validation("Execution.NotStarted", $"{execution} 尚未开始，不能停止。");
}
