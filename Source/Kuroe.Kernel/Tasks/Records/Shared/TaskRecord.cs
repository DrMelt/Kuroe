using Kuroe.Execution.Shared;
using Kuroe.Execution.Records.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Tasks.Records.Shared;

/// <summary>任务的完整存档：身份、保存时刻、生命周期、完成事实、过程记录、流程记录与执行记录。</summary>
public sealed record TaskRecord(
    TaskIdentity Identity,
    DateTimeOffset SavedAt,
    TaskLifecycle Lifecycle,
    bool Completed,
    TaskJournalRecord Journal,
    FlowRecord Flow,
    IReadOnlyList<ExecutionRecord> Executions);

/// <summary>任务的持久身份。</summary>
public sealed record TaskIdentity(
    TaskId Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);

/// <summary>任务级过程记录。</summary>
public sealed record TaskJournalRecord(
    IReadOnlyList<JournalEntry> Entries,
    int DroppedEntries);

/// <summary>任务锁定的流程记录：身份、定义与状态。</summary>
public sealed record FlowRecord(
    FlowIdentity Identity,
    IFlowDefinitionRecord Definition,
    IFlowStateRecord State);

/// <summary>流程定义的持久记录。由具体流程实现。</summary>
public interface IFlowDefinitionRecord;

/// <summary>流程状态的持久记录。由具体流程实现。</summary>
public interface IFlowStateRecord;