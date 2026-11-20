using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;

namespace Kuroe.Kernel.Execution.Shared.Request;

/// <summary>流程控制建立执行装配时交给执行参数构造的输入。</summary>
public sealed record ExecutionParameterContext(
    ExecutionOwnerId Owner,
    ExecutionStepId Step,
    int Attempt,
    int MaxAttempts,
    IReadOnlyList<ExecutionInputPort> InputPorts,
    IReadOnlyList<ExecutionInputValue> Inputs,
    IReadOnlyDictionary<ExecutionPortName, string> Texts,
    IReadOnlyList<ExecutionOutputPort> OutputPorts,
    string? Model);