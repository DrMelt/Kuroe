namespace Kuroe.Flow.Node.RuntimeGraph.Execution;

/// <summary>一次执行保留的恢复负载。格式由节点类型定义，流程层只保存与转发。</summary>
public sealed record ExecutionRestorePayload(
    string Format,
    string Payload);