using Kuroe.Execution.Shared.Request;

namespace Kuroe.Execution.Shared.Ports;

/// <summary>按执行请求给出的端口访问。</summary>
public sealed class ExecutionPortAccess(ExecutionRequest request) : IExecutionPortAccess
{
    /// <inheritdoc/>
    public IReadOnlyList<ExecutionOutputPort> Outputs => request.OutputPorts;

    /// <inheritdoc/>
    public string? Text(ExecutionPortName port) => request.Texts.GetValueOrDefault(port);
}