namespace Kuroe.Flow.Execution;

/// <summary>一次执行的只读面：标识、请求、状态、契约、产出与过程订阅。状态迁移只由执行核心完成。</summary>
public interface IExecution
{
    /// <summary>执行标识。</summary>
    ExecutionId Id { get; }

    /// <summary>本次执行的中性请求。</summary>
    ExecutionRequest Request { get; }

    /// <summary>执行创建时刻。</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>当前执行状态。</summary>
    ExecutionState State { get; }

    /// <summary>还在排队或正在执行。</summary>
    bool IsLive { get; }

    /// <summary>中断令牌。</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>本次执行的本轮契约状态。</summary>
    ExecutionContract Contract { get; }

    /// <summary>本次执行的产出。</summary>
    ExecutionOutput? Output { get; }

    /// <summary>本次执行保留的恢复数据。</summary>
    ExecutionRestoreState? RestoreData { get; }

    /// <summary>本次执行从哪次执行恢复。</summary>
    ExecutionId? ContinuedFrom { get; }

    /// <summary>快照可见的状态或过程变化。</summary>
    event Action? Changed;

    /// <summary>执行进入运行态后触发一次。</summary>
    event Action? Started;

    /// <summary>流式内容到达。</summary>
    event ProcessContentHandler? ContentEmitted;

    /// <summary>需要即时呈现的过程记录写入。</summary>
    event JournalEntryHandler? EntryEmitted;

    /// <summary>恢复数据变化。</summary>
    event Action? RestoreDataChanged;

    /// <summary>当前状态的只读快照。</summary>
    ExecutionSnapshot Snapshot();
}