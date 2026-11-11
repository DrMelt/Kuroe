namespace Kuroe.Flow.Execution;

/// <summary>执行输出端口：本次执行需要交回的结果槽位与触发承载。</summary>
public sealed record ExecutionOutputPort(
    ExecutionPortName Name,
    ExecutionPortKind Kind,
    bool Required,
    bool CarriesTrigger);