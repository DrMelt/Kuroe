using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime.Process;

/// <summary>任务级诊断记录。追加与读取共用一把锁，交出的快照不受后续追加影响。</summary>
internal sealed class Journal
{
    /// <summary>保留的记录条数，超出后丢最早的记录。</summary>
    private const int EntryLimit = 400;

    private const string ErrorLabel = "错误";
    private const string InfoLabel = "信息";

    private readonly Lock _gate = new();
    private readonly List<JournalEntry> _entries = [];
    private int _dropped;

    /// <summary>追加一条失败说明。</summary>
    public void AppendError(string text) =>
        Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Error, ErrorLabel, text));

    /// <summary>追加一条信息说明。</summary>
    public void AppendInfo(string text) =>
        Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, InfoLabel, text));

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

    /// <summary>追加一条过程记录。</summary>
    private void Append(JournalEntry entry)
    {
        lock (_gate)
        {
            AppendCore(entry);
        }
    }

    /// <summary>从检查点恢复记录与已丢弃条数，覆盖现有记录。</summary>
    public void Restore(IReadOnlyList<JournalEntry> entries, int dropped)
    {
        lock (_gate)
        {
            _entries.Clear();
            _entries.AddRange(entries);
            _dropped = dropped;
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
