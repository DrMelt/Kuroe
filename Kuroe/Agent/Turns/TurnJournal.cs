namespace Kuroe.Agent.Turns;

/// <summary>追加式过程记录。追加与读取共用一把锁，交出的快照不受后续追加影响。</summary>
public sealed class TurnJournal
{
    /// <summary>单条文本记录的上限，超出后另起一条，避免逐字增量反复拷贝整段。</summary>
    private const int SegmentLimit = 4096;

    /// <summary>保留的记录条数，超出后丢最早的记录。</summary>
    private const int EntryLimit = 400;

    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];
    private int _dropped;

    internal void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            AppendCore(entry);
        }
    }

    /// <summary>追加文本增量，与末尾的文本记录合并。</summary>
    internal void AppendText(string delta)
    {
        if (delta.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_entries.Count > 0 && _entries[^1] is TextEntry last && last.Text.Length + delta.Length <= SegmentLimit)
            {
                _entries[^1] = last with { Text = last.Text + delta };
                return;
            }

            AppendCore(new TextEntry(delta));
        }
    }

    /// <summary>已记录的条目，按发生顺序。</summary>
    public IReadOnlyList<JournalEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>因超出保留条数被丢弃的条数。</summary>
    public int DroppedEntries
    {
        get
        {
            lock (_gate)
            {
                return _dropped;
            }
        }
    }

    /// <summary>要求持有 <see cref="_gate"/>。</summary>
    private void AppendCore(JournalEntry entry)
    {
        _entries.Add(entry);
        if (_entries.Count > EntryLimit)
        {
            _entries.RemoveAt(0);
            _dropped++;
        }
    }
}
