namespace Kuroe.Flow.Shared.Execution.Turns;

/// <summary>一次回合的过程记录：追加内容，按发生顺序读出。</summary>
public interface ITurnJournal
{
    /// <summary>追加一条过程记录。</summary>
    void Append(JournalEntry entry);

    /// <summary>追加文本增量，与末尾的文本记录合并。</summary>
    void AppendText(string delta);

    /// <summary>已记录的条目，按发生顺序。</summary>
    IReadOnlyList<JournalEntry> Entries { get; }

    /// <summary>因超出保留条数被丢弃的条数。</summary>
    int DroppedEntries { get; }
}
