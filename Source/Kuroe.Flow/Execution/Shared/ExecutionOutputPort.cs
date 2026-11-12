namespace Kuroe.Flow.Execution;

/// <summary>执行输出端口：本次执行需要交回的结果槽位、触发承载与交回能力。</summary>
public sealed record ExecutionOutputPort(
    ExecutionPortName Name,
    ExecutionPortKind Kind,
    bool Required,
    bool CarriesTrigger,
    bool RequiresSubmission = false,
    bool IsConstant = false,
    string? Description = null);