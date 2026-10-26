namespace Kuroe.Flow.Shared.Execution.Process;

/// <summary>一次执行的过程记录，提供读取与过程通知。具体内容与格式由执行器写入，引擎只按本契约取用。
/// 事件在记录写入完成后、释放记录自身的锁之后触发。</summary>
public interface IProcessJournal
{
    /// <summary>已记录的条目，按发生顺序。</summary>
    IReadOnlyList<JournalEntry> Entries { get; }

    /// <summary>因超出保留条数被丢弃的条数。</summary>
    int DroppedEntries { get; }

    /// <summary>文本追加后给出该增量。</summary>
    event Action<string>? TextAppended;

    /// <summary>工具调用记录后给出该记录。</summary>
    event Action<ToolCallRecord>? ToolCallRecorded;
}