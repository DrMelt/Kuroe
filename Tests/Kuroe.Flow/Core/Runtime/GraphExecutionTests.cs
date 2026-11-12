using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;
using IExecutionRestoreDataSource = Kuroe.Flow.Execution.IExecutionRestoreDataSource;
using IExecutionUsageSource = Kuroe.Flow.Execution.IExecutionUsageSource;
using ExecutionRestoreState = Kuroe.Flow.Execution.ExecutionRestoreState;
using ExecutionUsage = Kuroe.Flow.Execution.ExecutionUsage;
using NeutralExecutionOutput = Kuroe.Flow.Execution.ExecutionOutput;
using TokenUsage = Kuroe.Flow.Execution.TokenUsage;

namespace Kuroe.Flow.Runtime.Tests;

/// <summary>执行的过程记录与过程事件：附加读取视图、播发、进度与收口后停止。</summary>
public sealed class GraphExecutionTests
{
    [Fact]
    public void Snapshot_without_journal_has_no_entries()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());

        GraphExecutionSnapshot snapshot = execution.Snapshot();

        Assert.Equal(ExecutionState.Queued, snapshot.State);
        Assert.Empty(snapshot.Journal);
        Assert.False(snapshot.HasRestoreData);
    }

    [Fact]
    public void Successful_settle_keeps_restore_data()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        TestRestoreReader reader = new() { RestoreData = new ExecutionRestoreState("test", "data") };

        execution.AttachRestoreDataReader(reader);
        execution.MarkRunning();
        execution.MarkSucceeded(NeutralExecutionOutput.FromPorts([]));
        execution.SettleRestoreData(reader);

        GraphExecutionSnapshot snapshot = execution.Snapshot();
        Assert.True(snapshot.HasRestoreData);
        Assert.Equal(new ExecutionRestorePayload("test", "data"), execution.RestoreData);
    }

    [Fact]
    public void Settling_without_data_clears_restore_data()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        TestRestoreReader reader = new() { RestoreData = new ExecutionRestoreState("test", "data") };

        execution.AttachRestoreDataReader(reader);
        reader.RestoreData = null;
        execution.SettleRestoreData(reader);

        GraphExecutionSnapshot snapshot = execution.Snapshot();
        Assert.False(snapshot.HasRestoreData);
        Assert.Null(execution.RestoreData);
    }

    [Fact]
    public void Attached_journal_appears_in_snapshot()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        JournalEntry entry = new(TestTime.At, JournalLevel.Information, "信息", "记录");
        TestJournal journal = new([entry]);

        execution.AttachJournal(journal);
        GraphExecutionSnapshot snapshot = execution.Snapshot();

        Assert.Same(entry, Assert.Single(snapshot.Journal));
    }

    [Fact]
    public void Attached_usage_reader_appears_in_snapshot()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        TestUsageReader reader = new() { Usage = new ExecutionUsage(1, 0, new TokenUsage(10, 2, 12, null, null)) };

        execution.AttachUsageReader(reader);

        Assert.Equal(new ExecutionUsage(1, 0, new TokenUsage(10, 2, 12, null, null)), execution.Snapshot().Usage);

        reader.Usage = new ExecutionUsage(2, 0, new TokenUsage(30, 4, 34, null, null));

        Assert.Equal(new ExecutionUsage(2, 0, new TokenUsage(30, 4, 34, null, null)), execution.Snapshot().Usage);
    }

    [Fact]
    public void Settled_usage_stops_following_the_reader()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        TestUsageReader reader = new() { Usage = new ExecutionUsage(1, 0, new TokenUsage(10, 2, 12, null, null)) };
        ExecutionUsage settled = new(2, 0, new TokenUsage(30, 4, 34, null, null));

        execution.AttachUsageReader(reader);
        execution.SettleUsage(new ExecutionUsage(2, 0, new TokenUsage(30, 4, 34, null, null)));
        reader.Usage = new ExecutionUsage(3, 0, new TokenUsage(50, 6, 56, null, null));

        Assert.Equal(settled, execution.Snapshot().Usage);
    }

    [Fact]
    public void Journal_content_entry_and_progress_reach_subscribers()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
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
            TestTime.At,
            JournalLevel.Information,
            "工具",
            "GetLocalTime {}\n2026-10-18 19:10:48");
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
    public void Progress_clearing_reaches_snapshot()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        execution.AttachJournal(journal);
        execution.MarkRunning();

        journal.EmitProgress("调用工具 GetLocalTime");
        journal.EmitProgress(string.Empty);

        Assert.Equal(string.Empty, execution.Snapshot().Progress);
    }

    [Fact]
    public void Settled_execution_stops_events_and_keeps_journal()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([new JournalEntry(TestTime.At, JournalLevel.Information, "回复", "保留")]);
        execution.AttachJournal(journal);
        int contents = 0;
        int entries = 0;
        execution.ContentEmitted += _ => contents++;
        execution.EntryEmitted += _ => entries++;
        execution.MarkRunning();
        execution.MarkSucceeded(NeutralExecutionOutput.FromPorts([]));

        journal.EmitContent(ProcessContentKind.Reply, "后");
        journal.EmitContent(ProcessContentKind.Reasoning, "后想");
        journal.EmitEntry(new JournalEntry(TestTime.At, JournalLevel.Information, "工具", "GetLocalTime {}"));
        journal.EmitProgress("调用工具 GetLocalTime");

        Assert.Equal(0, contents);
        Assert.Equal(0, entries);
        Assert.Equal(string.Empty, execution.Snapshot().Progress);
        Assert.Single(execution.Snapshot().Journal);
    }

    [Fact]
    public void Every_subscriber_receives_content_and_entries()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
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
        JournalEntry entry = new(TestTime.At, JournalLevel.Information, "工具", "GetLocalTime {}");
        journal.EmitEntry(entry);

        Assert.Equal([new ProcessContent(ProcessContentKind.Reply, "增量")], firstContents);
        Assert.Equal([new ProcessContent(ProcessContentKind.Reply, "增量")], secondContents);
        Assert.Equal([entry], firstEntries);
        Assert.Equal([entry], secondEntries);
    }

    [Fact]
    public void Subscription_after_settle_receives_nothing()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        TestJournal journal = new([]);
        execution.AttachJournal(journal);
        execution.MarkRunning();
        execution.MarkSucceeded(NeutralExecutionOutput.FromPorts([]));
        List<ProcessContent> contents = [];
        List<JournalEntry> entries = [];
        execution.ContentEmitted += contents.Add;
        execution.EntryEmitted += entries.Add;

        journal.EmitContent(ProcessContentKind.Reply, "后");
        journal.EmitContent(ProcessContentKind.Reasoning, "后想");
        journal.EmitEntry(new JournalEntry(TestTime.At, JournalLevel.Information, "工具", "GetLocalTime {}"));

        Assert.Empty(contents);
        Assert.Empty(entries);
    }

    [Fact]
    public void Started_event_fires_once_when_running()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        int started = 0;
        execution.Started += () => started++;

        execution.MarkRunning();
        execution.MarkRunning();

        Assert.Equal(1, started);
        Assert.NotNull(execution.Snapshot().StartedAt);
    }

    [Fact]
    public void StopExecution_rejects_an_execution_that_never_started()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());

        ErrorOr<Success> stopped = TaskDriver.StopExecution(execution);

        Assert.True(stopped.IsError);
        Assert.Equal("Execution.NotStarted", stopped.FirstError.Code);
    }

    [Fact]
    public void StopExecution_interrupts_a_started_execution()
    {
        GraphExecution execution = new(new ExecutionId(1), Input());
        execution.MarkRunning();

        ErrorOr<Success> stopped = TaskDriver.StopExecution(execution);

        Assert.False(stopped.IsError);
        execution.MarkInterrupted();
        Assert.Equal(ExecutionState.Interrupted, execution.Snapshot().State);
    }

    private static ExecutionInput Input() => new()
    {
        Task = new TaskId(1),
        Node = NodePath.Of("实施"),
        Type = TestNodeTypes.Executable,
    };

    private sealed class TestUsageReader : IExecutionUsageSource
    {
        public ExecutionUsage Usage { get; set; } = ExecutionUsage.None;
    }

    private sealed class TestRestoreReader : IExecutionRestoreDataSource
    {
        public ExecutionRestoreState? RestoreData { get; set; }

        public event Action? RestoreDataChanged
        {
            add => _ = value;
            remove => _ = value;
        }
    }

    private sealed class TestJournal(IReadOnlyList<JournalEntry> entries) : IProcessJournal
    {
        public IReadOnlyList<JournalEntry> Entries { get; } = entries;

        public event ProcessContentHandler? ContentAppended;

        public event JournalEntryHandler? EntryAppended;

        public event ProgressTextHandler? ProgressReported;

        public void EmitContent(ProcessContentKind kind, string delta) =>
            ContentAppended?.Invoke(new ProcessContent(kind, delta));

        public void EmitEntry(JournalEntry entry) => EntryAppended?.Invoke(entry);

        public void EmitProgress(string text) => ProgressReported?.Invoke(text);
    }
}