using ErrorOr;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Tasks;
using LoopExecution = Kuroe.Flow.Execution.Execution;

namespace Kuroe.Flow.Control;

/// <summary>流程控制可用的任务能力面：身份、控制状态、过程记录、执行集合与状态迁移。控制实例只经它读写任务。</summary>
public interface ITaskFlowContext
{
    /// <summary>任务内状态改动的串行点。</summary>
    Lock Gate { get; }

    /// <summary>任务标识。</summary>
    TaskId Id { get; }

    /// <summary>任务创建时刻。</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>提交时的开始输入。</summary>
    StartInputs Inputs { get; }

    /// <summary>任务标题。</summary>
    string Title { get; }

    /// <summary>任务控制状态。</summary>
    TaskLifecycle Lifecycle { get; }

    /// <summary>任务推进循环已退出且任务真正走完。</summary>
    bool Completed { get; }

    /// <summary>任务控制代次。</summary>
    int ControlVersion { get; }

    /// <summary>任务的全部执行，按提交顺序。</summary>
    IReadOnlyList<LoopExecution> Executions { get; }

    /// <summary>任务的过程记录。</summary>
    IReadOnlyList<JournalEntry> JournalEntries { get; }

    /// <summary>存档恢复时丢弃的过程记录条数。</summary>
    int DroppedJournal { get; }

    /// <summary>追加一条信息记录。</summary>
    void AppendInfo(string text);

    /// <summary>追加一条错误记录。</summary>
    void AppendError(string text);

    /// <summary>上报一条执行事件。</summary>
    void Report(ExecutionNotice notice);

    /// <summary>可恢复的中断收口时是否暂停任务。</summary>
    bool PauseOnRestore { get; }

    /// <summary>追加一条过程记录。</summary>
    void AppendJournal(JournalLevel level, string text);

    /// <summary>用存档内容覆盖过程记录。</summary>
    void RestoreJournal(IReadOnlyList<JournalEntry> entries, int dropped);

    /// <summary>通知任务状态已变。</summary>
    void NotifyChanged();

    /// <summary>记一次任务活动。</summary>
    void Touch();

    /// <summary>标记任务已走完。</summary>
    void MarkCompleted();

    /// <summary>暂停任务。</summary>
    ErrorOr<Success> Pause();

    /// <summary>恢复暂停的任务。</summary>
    ErrorOr<Success> Resume();

    /// <summary>终止任务。</summary>
    ErrorOr<Success> Terminate();

    /// <summary>可恢复的中断收口后暂停任务。</summary>
    void PauseForRestore();

    /// <summary>执行收口后的通知。</summary>
    void NotifyExecutionSettled(LoopExecution execution);

    /// <summary>订阅执行收口通知，返回解除订阅的委托。</summary>
    Action SubscribeExecutionSettled(Action<LoopExecution> handler);

    /// <summary>订阅新执行启动的通知，返回解除订阅的委托。</summary>
    Action SubscribeExecutionAttached(Action<LoopExecution> handler);

    /// <summary>在任务状态锁下执行一段操作。</summary>
    T UnderLock<T>(Func<T> action);
}