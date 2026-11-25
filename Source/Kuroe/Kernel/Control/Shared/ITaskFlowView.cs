using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>流程表现与任务快照可读的任务面。只含读取，不含任务状态改动。</summary>
public interface ITaskFlowView
{
    /// <summary>任务标识。</summary>
    TaskId Id { get; }

    /// <summary>任务创建时刻。</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>最近活动时刻。</summary>
    DateTimeOffset LastActivityAt { get; }

    /// <summary>提交时的开始输入。</summary>
    StartInputs Inputs { get; }

    /// <summary>流程运行状态，由具体流程实现解释。</summary>
    IFlowState FlowState { get; }

    /// <summary>任务标题。</summary>
    string Title { get; }

    /// <summary>任务的整体状态。</summary>
    TaskState State { get; }

    /// <summary>任务控制状态。</summary>
    TaskLifecycle Lifecycle { get; }

    /// <summary>任务的全部执行，按提交顺序。</summary>
    IReadOnlyList<IExecution> Executions { get; }

    /// <summary>任务的过程记录。</summary>
    IReadOnlyList<JournalEntry> JournalEntries { get; }

    /// <summary>存档恢复时丢弃的过程记录条数。</summary>
    int DroppedJournal { get; }

    /// <summary>此刻等待外部输入的步骤。</summary>
    IReadOnlyList<FlowPendingStep> Pending { get; }
}