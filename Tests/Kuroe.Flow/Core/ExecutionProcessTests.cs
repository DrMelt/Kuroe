using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>执行的过程记录与过程事件：附加读取视图、播发、进度与收口后停止。</summary>
public sealed class ExecutionProcessTests
{
    [Fact]
    public void Snapshot_without_journal_has_no_entries()
    {
        Execution execution = new(new ExecutionId(1), Input());

        ExecutionSnapshot snapshot = execution.Snapshot();

        Assert.Equal(ExecutionState.Queued, snapshot.State);
        Assert.Empty(snapshot.Journal);
        Assert.Equal(0, snapshot.DroppedEntries);
    }

    [Fact]
    public void Attached_journal_appears_in_snapshot()
    {
        Execution execution = new(new ExecutionId(1), Input());
        JournalEntry entry = new(DateTimeOffset.UtcNow, JournalLevel.Information, "信息", "记录");
        TestJournal journal = new([entry], 3);

        execution.AttachJournal(journal);
        ExecutionSnapshot snapshot = execution.Snapshot();

        Assert.Same(entry, Assert.Single(snapshot.Journal));
        Assert.Equal(3, snapshot.DroppedEntries);
    }

    [Fact]
    public void Journal_text_entry_and_progress_reach_subscribers()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        List<string> texts = [];
        List<JournalEntry> entries = [];
        execution.AttachJournal(journal);
        execution.TextEmitted += texts.Add;
        execution.EntryEmitted += entries.Add;
        execution.MarkRunning();

        journal.EmitText("增量");
        JournalEntry entry = new(
            DateTimeOffset.UtcNow,
            JournalLevel.Information,
            "工具",
            "GetLocalTime {}\n结果 2026-10-18 19:10:48");
        journal.EmitEntry(entry);
        journal.EmitProgress("调用工具 GetLocalTime");

        Assert.Equal(["增量"], texts);
        Assert.Equal([entry], entries);
        Assert.Equal("调用工具 GetLocalTime", execution.Snapshot().Progress);
    }

    [Fact]
    public void Settled_execution_stops_events_and_keeps_journal()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, "回复", "保留")]);
        execution.AttachJournal(journal);
        int texts = 0;
        int entries = 0;
        execution.TextEmitted += _ => texts++;
        execution.EntryEmitted += _ => entries++;
        execution.MarkRunning();
        execution.MarkSucceeded();

        journal.EmitText("后");
        journal.EmitEntry(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, "工具", "GetLocalTime {}"));
        journal.EmitProgress("调用工具 GetLocalTime");

        Assert.Equal(0, texts);
        Assert.Equal(0, entries);
        Assert.Equal(string.Empty, execution.Snapshot().Progress);
        Assert.Single(execution.Snapshot().Journal);
    }

    [Fact]
    public void Every_subscriber_receives_text_and_entries()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        List<string> firstTexts = [];
        List<string> secondTexts = [];
        List<JournalEntry> firstEntries = [];
        List<JournalEntry> secondEntries = [];
        execution.AttachJournal(journal);
        execution.TextEmitted += firstTexts.Add;
        execution.TextEmitted += secondTexts.Add;
        execution.EntryEmitted += firstEntries.Add;
        execution.EntryEmitted += secondEntries.Add;
        execution.MarkRunning();

        journal.EmitText("增量");
        JournalEntry entry = new(DateTimeOffset.UtcNow, JournalLevel.Information, "工具", "GetLocalTime {}");
        journal.EmitEntry(entry);

        Assert.Equal(["增量"], firstTexts);
        Assert.Equal(["增量"], secondTexts);
        Assert.Equal([entry], firstEntries);
        Assert.Equal([entry], secondEntries);
    }

    [Fact]
    public void Subscription_after_settle_receives_nothing()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        execution.AttachJournal(journal);
        execution.MarkRunning();
        execution.MarkSucceeded();
        List<string> texts = [];
        List<JournalEntry> entries = [];
        execution.TextEmitted += texts.Add;
        execution.EntryEmitted += entries.Add;

        journal.EmitText("后");
        journal.EmitEntry(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, "工具", "GetLocalTime {}"));

        Assert.Empty(texts);
        Assert.Empty(entries);
    }

    private static ExecutionInput Input() => new()
    {
        Task = new TaskId(1),
        Node = NodePath.Of("实施"),
        Type = TestNodeTypes.Executable,
    };

    private sealed class TestJournal(IReadOnlyList<JournalEntry> entries, int dropped = 0) : IProcessJournal
    {
        public IReadOnlyList<JournalEntry> Entries { get; } = entries;

        public int DroppedEntries { get; } = dropped;

        public event Action<string>? TextAppended;

        public event Action<JournalEntry>? EntryAppended;

        public event Action<string>? ProgressReported;

        public void EmitText(string delta) => TextAppended?.Invoke(delta);

        public void EmitEntry(JournalEntry entry) => EntryAppended?.Invoke(entry);

        public void EmitProgress(string text) => ProgressReported?.Invoke(text);
    }
}