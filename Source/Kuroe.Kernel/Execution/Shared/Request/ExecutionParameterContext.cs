using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;

namespace Kuroe.Execution.Shared.Request;

/// <summary>流程控制建立执行目标时交给目标类型的输入。</summary>
public sealed record ExecutionParameterContext(
    ExecutionOwnerId Owner,
    ExecutionTargetId Target,
    int Attempt,
    int MaxAttempts,
    IReadOnlyList<ExecutionInputPort> InputPorts,
    IReadOnlyList<ExecutionInputValue> Inputs,
    IReadOnlyDictionary<ExecutionPortName, string> Texts,
    IReadOnlyList<ExecutionOutputPort> OutputPorts,
    string? Model);