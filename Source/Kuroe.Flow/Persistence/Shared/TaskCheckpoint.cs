using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>当前检查点文档版本。加载时版本不符即拒绝。</summary>
public static class TaskCheckpointVersion
{
    /// <summary>当前版本。</summary>
    public const int Current = 1;
}

/// <summary>持久化的任务检查点文档：版本与任务状态。</summary>
/// <param name="Version">检查点版本。</param>
/// <param name="Task">任务检查点。</param>
public sealed record TaskCheckpointDocument(int Version, TaskCheckpoint Task);

/// <summary>任务在某一刻的完整检查点：身份、流程快照、开始状态、控制状态、完成事实、执行历史、节点状态与调度种子。</summary>
public sealed record TaskCheckpoint(
    TaskIdentity Identity,
    FlowSnapshot Flow,
    TaskStartSnapshot Start,
    TaskLifecycle Lifecycle,
    bool Completed,
    TaskJournalSnapshot Journal,
    IReadOnlyList<ExecutionCheckpoint> Executions,
    IReadOnlyList<NodeCheckpoint> Nodes,
    SchedulerSeed Pending);

/// <summary>任务的持久身份。</summary>
public sealed record TaskIdentity(
    TaskId Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);

/// <summary>任务开始时锁定的输入：提交的开始输入与宿主开始端口文本。</summary>
public sealed record TaskStartSnapshot(
    StartInputs Inputs,
    IReadOnlyDictionary<PortName, string> StartTexts);

/// <summary>任务级过程记录。</summary>
public sealed record TaskJournalSnapshot(
    IReadOnlyList<JournalEntry> Entries,
    int DroppedEntries);

/// <summary>恢复任务时推进循环尚未处理的节点。</summary>
public sealed record SchedulerSeed(
    IReadOnlyList<NodePath> Targets);
