using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime;

/// <summary>执行的过程记录与过程事件：附加读取视图、播发、进度与收口后停止。</summary>
public sealed class ExecutionTests
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
    public void Journal_content_entry_and_progress_reach_subscribers()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        List<ProcessContent> contents = [];
        List<JournalEntry> entries = [];
        execution.AttachJournal(journal);
        execution.ContentEmitted += contents.Add;
        execution.EntryEmitted += entries.Add;
        execution.MarkRunning();

        journal.EmitContent(ProcessContentKind.Reply, "增量");
        journal.EmitContent(ProcessContentKind.Reasoning, "思考");
        JournalEntry entry = new(
            DateTimeOffset.UtcNow,
            JournalLevel.Information,
            "工具",
            "GetLocalTime {}\n结果 2026-10-18 19:10:48");
        journal.EmitEntry(entry);
        journal.EmitProgress("调用工具 GetLocalTime");

        Assert.Equal(
            [
                new ProcessContent(ProcessContentKind.Reply, "增量"),
                new ProcessContent(ProcessContentKind.Reasoning, "思考"),
            ],
            contents);
        Assert.Equal([entry], entries);
        Assert.Equal("调用工具 GetLocalTime", execution.Snapshot().Progress);
    }

    [Fact]
    public void Settled_execution_stops_events_and_keeps_journal()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, "回复", "保留")]);
        execution.AttachJournal(journal);
        int contents = 0;
        int entries = 0;
        execution.ContentEmitted += _ => contents++;
        execution.EntryEmitted += _ => entries++;
        execution.MarkRunning();
        execution.MarkSucceeded();

        journal.EmitContent(ProcessContentKind.Reply, "后");
        journal.EmitContent(ProcessContentKind.Reasoning, "后想");
        journal.EmitEntry(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, "工具", "GetLocalTime {}"));
        journal.EmitProgress("调用工具 GetLocalTime");

        Assert.Equal(0, contents);
        Assert.Equal(0, entries);
        Assert.Equal(string.Empty, execution.Snapshot().Progress);
        Assert.Single(execution.Snapshot().Journal);
    }

    [Fact]
    public void Every_subscriber_receives_content_and_entries()
    {
        Execution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        List<ProcessContent> firstContents = [];
        List<ProcessContent> secondContents = [];
        List<JournalEntry> firstEntries = [];
        List<JournalEntry> secondEntries = [];
        execution.AttachJournal(journal);
        execution.ContentEmitted += firstContents.Add;
        execution.ContentEmitted += secondContents.Add;
        execution.EntryEmitted += firstEntries.Add;
        execution.EntryEmitted += secondEntries.Add;
        execution.MarkRunning();

        journal.EmitContent(ProcessContentKind.Reply, "增量");
        JournalEntry entry = new(DateTimeOffset.UtcNow, JournalLevel.Information, "工具", "GetLocalTime {}");
        journal.EmitEntry(entry);

        Assert.Equal([new ProcessContent(ProcessContentKind.Reply, "增量")], firstContents);
        Assert.Equal([new ProcessContent(ProcessContentKind.Reply, "增量")], secondContents);
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
        List<ProcessContent> contents = [];
        List<JournalEntry> entries = [];
        execution.ContentEmitted += contents.Add;
        execution.EntryEmitted += entries.Add;

        journal.EmitContent(ProcessContentKind.Reply, "后");
        journal.EmitContent(ProcessContentKind.Reasoning, "后想");
        journal.EmitEntry(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, "工具", "GetLocalTime {}"));

        Assert.Empty(contents);
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

        public event Action<ProcessContent>? ContentAppended;

        public event Action<JournalEntry>? EntryAppended;

        public event Action<string>? ProgressReported;

        public void EmitContent(ProcessContentKind kind, string delta) =>
            ContentAppended?.Invoke(new ProcessContent(kind, delta));

        public void EmitEntry(JournalEntry entry) => EntryAppended?.Invoke(entry);

        public void EmitProgress(string text) => ProgressReported?.Invoke(text);
    }
}