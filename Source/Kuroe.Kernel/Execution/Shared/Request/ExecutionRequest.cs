using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;

namespace Kuroe.Execution.Shared.Request;

/// <summary>一次执行的输入：归属、目标、执行端口与执行可读文本。</summary>
public sealed record ExecutionRequest(
    ExecutionOwnerId Owner,
    ExecutionTargetId Target,
    IReadOnlyList<ExecutionInputPort> InputPorts,
    IReadOnlyList<ExecutionInputValue> Inputs,
    IReadOnlyDictionary<ExecutionPortName, string> Texts,
    IReadOnlyList<ExecutionOutputPort> OutputPorts);