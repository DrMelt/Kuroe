namespace Kuroe.Execution.Shared;

/// <summary>执行输入端口：本次执行接受的输入通道、角色与默认文本。</summary>
public sealed record ExecutionInputPort(
    ExecutionPortName Name,
    ExecutionPortKind Kind,
    ExecutionPortRole Role,
    bool Required,
    string? DefaultText);