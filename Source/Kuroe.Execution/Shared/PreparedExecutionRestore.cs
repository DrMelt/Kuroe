namespace Kuroe.Execution.Shared;

/// <summary>一次恢复的上下文与目标类型解码出的执行数据。</summary>
public sealed record PreparedExecutionRestore(
    ExecutionRestoreContext Context,
    IExecutionRestoreData Data);