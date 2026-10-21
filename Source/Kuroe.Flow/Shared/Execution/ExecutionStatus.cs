using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>一次执行在本轮的契约状态：节点身份、轮次与上限、节点状态、尚缺的交回项与已上报的无法完成原因。</summary>
public sealed record ExecutionStatus(
    NodePath Node,
    int ExecutionCount,
    int MaxExecutions,
    NodeState NodeState,
    IReadOnlyList<PortName> OutstandingPorts,
    string? CannotCompleteReason);