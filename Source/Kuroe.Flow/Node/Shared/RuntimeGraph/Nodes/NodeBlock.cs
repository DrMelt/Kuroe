using Kuroe.Flow.Execution.Shared;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;

/// <summary>节点停在阻塞时的原因、可恢复的关联执行与时刻。关联为空表示当前阻塞不可恢复。</summary>
public sealed record NodeBlock(
    string Summary,
    ExecutionId? Execution,
    DateTimeOffset At);