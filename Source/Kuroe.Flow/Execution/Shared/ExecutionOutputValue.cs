namespace Kuroe.Flow.Execution;

/// <summary>一次执行在一个输出端口上交回的值。触发位表达是否驱动下游。</summary>
public sealed record ExecutionOutputValue(
    ExecutionPortName Port,
    string? Text,
    bool Fires = true);