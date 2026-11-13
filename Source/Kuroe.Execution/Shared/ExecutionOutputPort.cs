namespace Kuroe.Execution.Shared;

/// <summary>执行输出端口：本次执行需要交回的结果槽位、触发承载与契约说明。</summary>
public sealed record ExecutionOutputPort(
    ExecutionPortName Name,
    ExecutionPortKind Kind,
    bool Required,
    bool CarriesTrigger,
    string? Description = null);