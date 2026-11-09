namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次恢复的输入与节点数据。输入由引擎装配，数据由节点类型解码。</summary>
public sealed record PreparedRestore(
    ExecutionInput Input,
    IExecutionRestoreData Data);