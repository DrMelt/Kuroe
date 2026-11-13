using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Ports;

namespace Kuroe.Flow.Node.RuntimeGraph.Execution;

/// <summary>一次执行的本轮契约状态：已交回的端口值与已上报的无法完成原因。</summary>
public sealed record ExecutionContract(
    IReadOnlyDictionary<PortName, PortValue>? Ports = null,
    string? CannotCompleteReason = null)
{
    /// <summary>没有交回端口、没有上报无法完成的契约。</summary>
    public static ExecutionContract Empty { get; } = new();
}