namespace Kuroe.Flow.Shared.Execution.Process;

/// <summary>一次执行的过程记录，只提供读取。内容由节点侧执行体写入，引擎只按本契约取用。</summary>
public interface IProcessJournal
{
    /// <summary>已记录的条目，按发生顺序。</summary>
    IReadOnlyList<JournalEntry> Entries { get; }

    /// <summary>因超出保留条数被丢弃的条数。</summary>
    int DroppedEntries { get; }
}