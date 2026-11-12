using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime.Process;

/// <summary>任务级诊断记录。追加与读取共用共享存储，交出的快照不受后续追加影响。</summary>
internal sealed class Journal
{
    private const string ErrorLabel = "错误";
    private const string InfoLabel = "信息";

    private readonly ProcessJournalStore _store = new();

    /// <summary>追加一条失败说明。</summary>
    public void AppendError(string text) =>
        _store.Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Error, ErrorLabel, text));

    /// <summary>追加一条信息说明。</summary>
    public void AppendInfo(string text) =>
        _store.Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, InfoLabel, text));

    /// <summary>已记录的条目，按发生顺序。</summary>
    public IReadOnlyList<JournalEntry> Entries => _store.Entries;

    /// <summary>存档恢复的丢弃条数。新记录不丢弃条目。</summary>
    public int DroppedEntries => _store.DroppedEntries;

    /// <summary>从存档恢复记录与已丢弃条数，覆盖现有记录。</summary>
    public void Restore(IReadOnlyList<JournalEntry> entries, int dropped) =>
        _store.Replace(entries, dropped);
}
