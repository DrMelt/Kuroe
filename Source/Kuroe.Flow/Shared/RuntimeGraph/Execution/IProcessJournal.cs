namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行的过程记录，提供读取与过程通知。条目由执行器按通用结构写入，标签由执行器决定，取用方只按时间、级别、标签与文本形态使用。
/// 事件在相应变更完成后、释放记录自身的锁之后触发。</summary>
public interface IProcessJournal
{
    /// <summary>已记录的条目，按发生顺序。</summary>
    IReadOnlyList<JournalEntry> Entries { get; }

    /// <summary>因超出保留条数被丢弃的条数。</summary>
    int DroppedEntries { get; }

    /// <summary>文本追加后给出该增量。</summary>
    event Action<string>? TextAppended;

    /// <summary>需要即时呈现的条目写入后给出该条目。</summary>
    event Action<JournalEntry>? EntryAppended;

    /// <summary>执行进度变化后给出进度文本。</summary>
    event Action<string>? ProgressReported;
}