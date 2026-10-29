
namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行对宿主的视图：所属上下文、状态快照与过程事件。状态改动只在引擎内部。</summary>
public interface IExecution
{
    /// <summary>执行标识。</summary>
    ExecutionId Id { get; }

    /// <summary>本次执行的输入。</summary>
    ExecutionInput Input { get; }

    /// <summary>当前执行状态。</summary>
    ExecutionState State { get; }

    /// <summary>当前状态的只读快照。</summary>
    ExecutionSnapshot Snapshot();

    /// <summary>取消本次执行，进行中的一轮请求随之结束。</summary>
    void Cancel();

    /// <summary>执行输出的一段文本，按到达顺序给出。收口后不再触发。</summary>
    event Action<string>? TextEmitted;

    /// <summary>一条需要即时呈现的过程记录。收口后不再触发。</summary>
    event Action<JournalEntry>? EntryEmitted;
}
