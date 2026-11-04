using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>节点停在阻塞时的原因、关联执行与时刻。</summary>
public sealed record NodeBlock(
    string Summary,
    ExecutionId? Execution,
    DateTimeOffset At);