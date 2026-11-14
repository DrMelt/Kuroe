using Kuroe.Flow.Node.Shared.RuntimeGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;

/// <summary>一次图执行的本轮契约状态：已交回的图端口值与已上报的无法完成原因。</summary>
public sealed record GraphExecutionContract(
    IReadOnlyDictionary<PortName, PortValue>? Ports = null,
    string? CannotCompleteReason = null)
{
    /// <summary>没有交回端口、没有上报无法完成的契约。</summary>
    public static GraphExecutionContract Empty { get; } = new();
}