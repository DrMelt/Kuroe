namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行留在存档中的恢复状态。格式由节点类型定义，流程层只保存与转发。</summary>
public sealed record ExecutionRestoreState(
    string Format,
    string Payload);