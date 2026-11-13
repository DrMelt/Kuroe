using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Node.RuntimeGraph.Tasks;

/// <summary>恢复任务时推进循环尚未处理的节点。</summary>
public sealed record SchedulerSeed(
    IReadOnlyList<NodePath> Targets);