using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime.Process;

/// <summary>从检查点恢复的只读过程记录，不产生新的过程事件。</summary>
internal sealed class RestoredProcessJournal(IReadOnlyList<JournalEntry> entries, int dropped) : IProcessJournal
{
    public IReadOnlyList<JournalEntry> Entries { get; } = entries;

    public int DroppedEntries { get; } = dropped;

    public event Action<ProcessContent>? ContentAppended
    {
        add => _ = value;
        remove => _ = value;
    }

    public event Action<JournalEntry>? EntryAppended
    {
        add => _ = value;
        remove => _ = value;
    }

    public event Action<string>? ProgressReported
    {
        add => _ = value;
        remove => _ = value;
    }
}
