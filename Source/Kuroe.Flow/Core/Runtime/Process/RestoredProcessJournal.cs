using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime.Process;

/// <summary>从存档恢复的只读过程记录，不产生新的过程事件。</summary>
internal sealed class RestoredProcessJournal(IReadOnlyList<JournalEntry> entries, int dropped) : IProcessJournal
{
    public IReadOnlyList<JournalEntry> Entries { get; } = entries;

    public int DroppedEntries { get; } = dropped;

    public event ProcessContentHandler? ContentAppended
    {
        add => _ = value;
        remove => _ = value;
    }

    public event JournalEntryHandler? EntryAppended
    {
        add => _ = value;
        remove => _ = value;
    }

    public event ProgressTextHandler? ProgressReported
    {
        add => _ = value;
        remove => _ = value;
    }
}
