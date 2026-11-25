using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Notice;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>流程控制可用的任务能力面：身份、控制状态、过程记录、执行集合与状态迁移。控制实例只经它读写任务。</summary>
public interface ITaskFlowContext : ITaskFlowView
{
    /// <summary>任务内状态改动的串行点。</summary>
    Lock Gate { get; }

    /// <summary>任务启动后的流程控制实例。</summary>
    IFlowControl? Control { get; }

    /// <summary>记下一个执行。要求持有 <see cref="Gate"/>。</summary>
    void AttachExecution(IExecution execution);

    /// <summary>任务推进循环已退出且任务真正走完。</summary>
    bool Completed { get; }

    /// <summary>任务控制代次。</summary>
    int ControlVersion { get; }

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
    void NotifyExecutionSettled(IExecution execution);

    /// <summary>订阅执行收口通知，返回解除订阅的委托。</summary>
    Action SubscribeExecutionSettled(Action<IExecution> handler);

    /// <summary>订阅新执行启动的通知，返回解除订阅的委托。</summary>
    Action SubscribeExecutionAttached(Action<IExecution> handler);

    /// <summary>在任务状态锁下执行一段操作。</summary>
    T UnderLock<T>(Func<T> action);
}