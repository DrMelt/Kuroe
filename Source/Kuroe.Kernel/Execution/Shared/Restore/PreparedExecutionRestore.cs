using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Shared.Restore;

/// <summary>一次恢复的装配结果、上下文与目标类型解码出的执行数据。</summary>
public sealed record PreparedExecutionRestore(
    ExecutionInvocation Invocation,
    ExecutionRestoreContext Context,
    IExecutionRestoreData Data);