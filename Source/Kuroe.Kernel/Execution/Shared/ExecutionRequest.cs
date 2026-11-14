namespace Kuroe.Execution.Shared;

/// <summary>一次执行的输入：归属、目标、轮次、执行端口与执行可读文本。</summary>
public sealed record ExecutionRequest(
    ExecutionOwnerId Owner,
    ExecutionTargetId Target,
    string Kind,
    int Attempt,
    IReadOnlyList<ExecutionInputPort> InputPorts,
    IReadOnlyList<ExecutionInputValue> Inputs,
    IReadOnlyDictionary<ExecutionPortName, string> Texts,
    IReadOnlyList<ExecutionOutputPort> OutputPorts,
    int MaxAttempts = 1);