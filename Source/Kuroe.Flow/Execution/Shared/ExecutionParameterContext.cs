namespace Kuroe.Flow.Execution;

/// <summary>流程控制建立执行目标时交给目标类型的输入。</summary>
public sealed record ExecutionParameterContext(
    ExecutionOwnerId Owner,
    ExecutionTargetId Target,
    string Kind,
    int Attempt,
    IReadOnlyList<ExecutionInputPort> InputPorts,
    IReadOnlyList<ExecutionInputValue> Inputs,
    IReadOnlyDictionary<ExecutionPortName, string> Texts,
    IReadOnlyList<ExecutionOutputPort> OutputPorts,
    string? Model);