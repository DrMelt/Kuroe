using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.Persistence.Records.Shared;

/// <summary>任务在某一刻的完整存档：身份、存档写入时刻、流程快照、开始状态、控制状态、完成事实、执行历史、节点状态与调度种子。</summary>
public sealed record TaskRecord(
    TaskIdentity Identity,
    DateTimeOffset SavedAt,
    FlowSnapshot Flow,
    TaskStartSnapshot Start,
    TaskLifecycle Lifecycle,
    bool Completed,
    TaskJournalSnapshot Journal,
    IReadOnlyList<ExecutionRecord> Executions,
    IReadOnlyList<NodeRecord> Nodes,
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
