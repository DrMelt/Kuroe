namespace Kuroe.Flow.Execution;

/// <summary>一次执行在一个输入端口上取到的内容。来源是不透明字符串，由流程控制解释。</summary>
public sealed record ExecutionInputValue(
    ExecutionPortName Port,
    string Text,
    string? Source);