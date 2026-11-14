namespace Kuroe.Execution.Shared;

/// <summary>执行契约的错误码。</summary>
public static class ExecutionErrorCodes
{
    /// <summary>执行号不存在。</summary>
    public const string NotFound = "Execution.NotFound";

    /// <summary>执行端口名不合法。</summary>
    public const string Port = "ExecutionPort";

    /// <summary>执行契约不合法。</summary>
    public const string Contract = "ExecutionContract";
}