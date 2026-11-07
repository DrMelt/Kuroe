namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次恢复的输入与节点状态。输入由引擎装配，状态由节点类型解码。</summary>
public sealed record PreparedRestore(
    ExecutionInput Input,
    IExecutionRestoreState State);