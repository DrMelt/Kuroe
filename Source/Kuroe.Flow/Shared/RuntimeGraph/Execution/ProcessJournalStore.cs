namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>过程记录的共享存储：追加、同标签增量合并、保留上限与快照。所有使用方共用同一套保留策略。</summary>
public sealed class ProcessJournalStore(int entryLimit = 400, int segmentLimit = 4096)
{
    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];
    private int _dropped;

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

    /// <summary>追加一条记录。</summary>
    public void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            Add(entry);
        }
    }

    /// <summary>追加同标签的文本增量，与末尾同类记录合并且不超过单条上限；空增量不写，返回是否写入。</summary>
    public bool AppendDelta(string label, string delta)
    {
        if (delta.Length == 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (_entries.Count > 0
                && _entries[^1].Label == label
                && _entries[^1].Text.Length + delta.Length <= segmentLimit)
            {
                JournalEntry last = _entries[^1];
                _entries[^1] = last with { Text = last.Text + delta };
            }
            else
            {
                Add(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, label, delta));
            }
        }

        return true;
    }

    /// <summary>用给定记录覆盖现有记录，用于从存档恢复。</summary>
    public void Replace(IReadOnlyList<JournalEntry> entries, int dropped)
    {
        lock (_gate)
        {
            _entries.Clear();
            _entries.AddRange(entries);
            _dropped = dropped;
        }
    }

    /// <summary>要求持有 <see cref="_gate"/>。</summary>
    private void Add(JournalEntry entry)
    {
        _entries.Add(entry);
        if (_entries.Count > entryLimit)
        {
            _entries.RemoveAt(0);
            _dropped++;
        }
    }
}